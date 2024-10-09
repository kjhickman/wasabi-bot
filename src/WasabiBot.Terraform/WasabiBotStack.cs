using Amazon.ECR;
using Amazon.ECR.Model;
using Constructs;
using HashiCorp.Cdktf;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Api;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Integration;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Route;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Stage;
using HashiCorp.Cdktf.Providers.Aws.IamRole;
using HashiCorp.Cdktf.Providers.Aws.IamRolePolicy;
using HashiCorp.Cdktf.Providers.Aws.IamRolePolicyAttachment;
using HashiCorp.Cdktf.Providers.Aws.LambdaEventSourceMapping;
using HashiCorp.Cdktf.Providers.Aws.LambdaFunction;
using HashiCorp.Cdktf.Providers.Aws.LambdaPermission;
using HashiCorp.Cdktf.Providers.Aws.Provider;
using HashiCorp.Cdktf.Providers.Aws.SqsQueue;
using WasabiBot.Terraform.Settings;
// ReSharper disable ObjectCreationAsStatement

namespace WasabiBot.Terraform;

internal class WasabiBotStack : TerraformStack
{
    public WasabiBotStack(Construct scope, string id, EnvironmentVariables vars) : base(scope, id)
    {
        var env = vars.ENVIRONMENT;
        const string region = "us-east-1";
        const string service = "wasabi-bot";
        
        new AwsProvider(this, "Aws", new AwsProviderConfig
        {
            Region = region
        });
        
        new S3Backend(this, new S3BackendConfig
        {
            Bucket = "tfstate-b5f4b976dc5f",
            Key = $"{service}.{env}.tfstate",
            Region = region,
            // TODO: add DynamoDB locking
        });
        
        var deferredDeadLetterQueue = new SqsQueue(this, "DeadLetterQueue", new SqsQueueConfig
        {
            Name = $"{env}-{service}-deferred-dlq",
            MessageRetentionSeconds = 750,
            VisibilityTimeoutSeconds = 30
        });
        
        var deferredQueue = new SqsQueue(this, "WasabiBotDeferredQueue", new SqsQueueConfig
        {
            Name = $"{env}-{service}-deferred",
            VisibilityTimeoutSeconds = 30,
            RedrivePolicy = $"{{\"deadLetterTargetArn\":\"{deferredDeadLetterQueue.Arn}\", \"maxReceiveCount\": 3}}"
        });

        var lambdaRole = new IamRole(this, "LambdaExecutionRole", new IamRoleConfig
        {
            Name = $"{env}-{service}-lambda-role",
            AssumeRolePolicy = """
                               {
                                 "Version": "2012-10-17",
                                 "Statement": [
                                   {
                                     "Effect": "Allow",
                                     "Principal": {
                                       "Service": "lambda.amazonaws.com"
                                     },
                                     "Action": "sts:AssumeRole"
                                   }
                                 ]
                               }
                               """
        });

        new IamRolePolicyAttachment(this, "LambdaPolicyAttachment", new IamRolePolicyAttachmentConfig
        {
            Role = lambdaRole.Name,
            PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
        });

        new IamRolePolicy(this, "LambdaSQSPolicy", new IamRolePolicyConfig
        {
            Name = $"{env}-lambda-sqs-policy",
            Role = lambdaRole.Name,
            Policy = $$"""
                       {
                         "Version": "2012-10-17",
                         "Statement": [
                           {
                             "Effect": "Allow",
                             "Action": [
                               "sqs:SendMessage",
                               "sqs:ReceiveMessage",
                               "sqs:DeleteMessage",
                               "sqs:GetQueueAttributes"
                             ],
                             "Resource": "{{deferredQueue.Arn}}"
                           }
                         ]
                       }
                       """
        });
        
        var latestDigest = GetLatestImageDigestAsync(service, env).Result;
        
        var lambdaEnvironmentVars = new Dictionary<string, string>
        {
            { nameof(vars.DISCORD_APPLICATION_ID), vars.DISCORD_APPLICATION_ID },
            { "DISCORD_DEFERRED_EVENT_QUEUE_URL", deferredQueue.Url },
            { nameof(vars.DISCORD_PUBLIC_KEY), vars.DISCORD_PUBLIC_KEY },
            { nameof(vars.DISCORD_TOKEN), vars.DISCORD_TOKEN },
            { "ConnectionStrings__Postgres", vars.NEON_CONNECTION_STRING },
        };
        
        var lambdaFunction = new LambdaFunction(this, "WasabiBotLambda", new LambdaFunctionConfig
        {
            FunctionName = $"{env}-{service}",
            ImageUri = $"{vars.AWS_ACCOUNT_ID}.dkr.ecr.{region}.amazonaws.com/{service}@{latestDigest}",
            PackageType = "Image",
            Architectures = [vars.ARCHITECTURE],
            MemorySize = 512,
            Timeout = 30,
            Environment = new LambdaFunctionEnvironment
            {
                Variables = lambdaEnvironmentVars
            },
            LoggingConfig = new LambdaFunctionLoggingConfig
            {
                LogFormat = "JSON"
            },
            Role = lambdaRole.Arn
        });

        new LambdaEventSourceMapping(this, "SqsToLambdaTrigger", new LambdaEventSourceMappingConfig
        {
            EventSourceArn = deferredQueue.Arn,
            FunctionName = lambdaFunction.FunctionName,
            BatchSize = 10,
            Enabled = true,
            FunctionResponseTypes = ["ReportBatchItemFailures"]
        });

        var httpApi = new Apigatewayv2Api(this, "WasabiBotHttpApi", new Apigatewayv2ApiConfig
        {
            Name = $"{env}-{service}-api",
            ProtocolType = "HTTP"
        });
        
        new LambdaPermission(this, "ApiGatewayInvokeLambdaPermission", new LambdaPermissionConfig
        {
            Action = "lambda:InvokeFunction",
            FunctionName = lambdaFunction.FunctionName,
            Principal = "apigateway.amazonaws.com",
            SourceArn = $"arn:aws:execute-api:{region}:{vars.AWS_ACCOUNT_ID}:{httpApi.Id}/*"
        });

        var lambdaIntegration = new Apigatewayv2Integration(this, "LambdaIntegration", new Apigatewayv2IntegrationConfig
        {
            ApiId = httpApi.Id,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = lambdaFunction.InvokeArn,
            PayloadFormatVersion = "2.0"
        });

        new Apigatewayv2Route(this, "ApiRoute", new Apigatewayv2RouteConfig
        {
            ApiId = httpApi.Id,
            RouteKey = "ANY /{proxy+}",
            Target = $"integrations/{lambdaIntegration.Id}"
        });

        new Apigatewayv2Stage(this, "ApiStage", new Apigatewayv2StageConfig
        {
            ApiId = httpApi.Id,
            Name = "$default",
            AutoDeploy = true
        });
        
        // Outputs
        new TerraformOutput(this, "DeferredQueueUrl", new TerraformOutputConfig
        {
            Value = deferredQueue.Url
        });

        new TerraformOutput(this, "ApiGatewayUrl", new TerraformOutputConfig
        {
            Value = httpApi.ApiEndpoint
        });
    }

    private async Task<string?> GetLatestImageDigestAsync(string repository, string tag)
    {
        var ecrClient = new AmazonECRClient();

        var request = new DescribeImagesRequest
        {
            RepositoryName = repository,
            Filter = new DescribeImagesFilter
            {
                TagStatus = "TAGGED"
            }
        };

        var response = await ecrClient.DescribeImagesAsync(request);
        
        var latestImage = response.ImageDetails.OrderByDescending(image => image.ImagePushedAt).FirstOrDefault(x => x.ImageTags.Contains(tag));

        return latestImage?.ImageDigest;
    }
}