using Constructs;
using HashiCorp.Cdktf;
using HashiCorp.Cdktf.Providers.Aws.AcmCertificate;
using HashiCorp.Cdktf.Providers.Aws.AcmCertificateValidation;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Api;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2ApiMapping;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2DomainName;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Integration;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Route;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2Stage;
using HashiCorp.Cdktf.Providers.Aws.Apigatewayv2VpcLink;
using HashiCorp.Cdktf.Providers.Aws.CloudwatchLogGroup;
using HashiCorp.Cdktf.Providers.Aws.EcsService;
using HashiCorp.Cdktf.Providers.Aws.EcsTaskDefinition;
using HashiCorp.Cdktf.Providers.Aws.IamRole;
using HashiCorp.Cdktf.Providers.Aws.IamRolePolicy;
using HashiCorp.Cdktf.Providers.Aws.Provider;
using HashiCorp.Cdktf.Providers.Aws.Route53Record;
using HashiCorp.Cdktf.Providers.Aws.SecurityGroup;
using HashiCorp.Cdktf.Providers.Aws.ServiceDiscoveryHttpNamespace;
using HashiCorp.Cdktf.Providers.Aws.ServiceDiscoveryService;
using HashiCorp.Cdktf.Providers.Aws.SnsTopic;
using HashiCorp.Cdktf.Providers.Aws.SnsTopicSubscription;
using HashiCorp.Cdktf.Providers.Aws.SqsQueue;
using HashiCorp.Cdktf.Providers.Aws.SqsQueuePolicy;
using WasabiBot.Terraform.Settings;

// ReSharper disable ObjectCreationAsStatement

namespace WasabiBot.Terraform.Stacks;

internal class WasabiBotStack : TerraformStack
{   
    public WasabiBotStack(Construct scope, string id, EnvironmentVariables vars) : base(scope, id)
    {
        var env = vars.ENVIRONMENT;
        const string region = "us-east-1";
        const string service = "wasabi-bot";

        var defaultTags = new AwsProviderDefaultTags
        {
            Tags = new Dictionary<string, string>
            {
                { "Environment", env },
                { "Service", service }
            }
        };

        new AwsProvider(this, "Aws", new AwsProviderConfig
        {
            Region = region,
            DefaultTags = new[] { defaultTags }
        });
        
        new S3Backend(this, new S3BackendConfig
        {
            Bucket = "tfstate-b5f4b976dc5f",
            Key = $"{service}.{env}.tfstate",
            Region = region,
            // TODO: add DynamoDB locking
        });
        
        // TODO: create reusable module for masstransit infra
        var interactionDeferredErrorQueue = new SqsQueue(this, "InteractionDeferredErrorQueue", new SqsQueueConfig
        {
            Name = $"{env}-{service}-interaction-deferred-error",
            MessageRetentionSeconds = 750,
            VisibilityTimeoutSeconds = 30
        });
        
        var interactionDeferredQueue = new SqsQueue(this, "InteractionDeferredQueue", new SqsQueueConfig
        {
            Name = $"{env}-{service}-interaction-deferred",
            VisibilityTimeoutSeconds = 30,
            RedrivePolicy = $"{{\"deadLetterTargetArn\":\"{interactionDeferredErrorQueue.Arn}\", \"maxReceiveCount\": 3}}"
        });
        
        var interactionDeferredTopic = new SnsTopic(this, "InteractionDeferredTopic", new SnsTopicConfig
        {
            Name = $"{env}-{service}-interaction-deferred",
        });

        var interactionDeferredSubscription = new SnsTopicSubscription(this, "InteractionDeferredSubscription", new SnsTopicSubscriptionConfig
        {
            TopicArn = interactionDeferredTopic.Arn,
            Protocol = "sqs",
            Endpoint = interactionDeferredQueue.Arn
        });
        
        new SqsQueuePolicy(this, "InteractionDeferredQueuePolicy", new SqsQueuePolicyConfig
        {
            QueueUrl = interactionDeferredQueue.Id,
            Policy = $$"""
                       {
                           "Version": "2012-10-17",
                           "Statement": [
                               {
                                   "Effect": "Allow",
                                   "Principal": {
                                       "Service": "sns.amazonaws.com"
                                   },
                                   "Action": "sqs:SendMessage",
                                   "Resource": "{{interactionDeferredQueue.Arn}}",
                                   "Condition": {
                                       "ArnEquals": {
                                           "aws:SourceArn": "{{interactionDeferredTopic.Arn}}"
                                       }
                                   }
                               }
                           ]
                       }
                       """
        });
        
        var interactionReceivedErrorQueue = new SqsQueue(this, "InteractionReceivedErrorQueue", new SqsQueueConfig
        {
            Name = $"{env}-{service}-interaction-received-error",
            MessageRetentionSeconds = 750,
            VisibilityTimeoutSeconds = 30
        });
        
        var interactionReceivedQueue = new SqsQueue(this, "InteractionReceivedQueue", new SqsQueueConfig
        {
            Name = $"{env}-{service}-interaction-received",
            VisibilityTimeoutSeconds = 30,
            RedrivePolicy = $"{{\"deadLetterTargetArn\":\"{interactionReceivedErrorQueue.Arn}\", \"maxReceiveCount\": 3}}"
        });
        
        var interactionReceivedTopic = new SnsTopic(this, "InteractionReceivedTopic", new SnsTopicConfig
        {
            Name = $"{env}-{service}-interaction-received",
        });
        
        var interactionReceivedSubscription = new SnsTopicSubscription(this, "InteractionReceivedSubscription", new SnsTopicSubscriptionConfig
        {
            TopicArn = interactionReceivedTopic.Arn,
            Protocol = "sqs",
            Endpoint = interactionReceivedQueue.Arn
        });

        new SqsQueuePolicy(this, "InteractionReceivedQueuePolicy", new SqsQueuePolicyConfig
        {
            QueueUrl = interactionReceivedQueue.Id,
            Policy = $$"""
                       {
                           "Version": "2012-10-17",
                           "Statement": [
                               {
                                   "Effect": "Allow",
                                   "Principal": {
                                       "Service": "sns.amazonaws.com"
                                   },
                                   "Action": "sqs:SendMessage",
                                   "Resource": "{{interactionReceivedQueue.Arn}}",
                                   "Condition": {
                                       "ArnEquals": {
                                           "aws:SourceArn": "{{interactionReceivedTopic.Arn}}"
                                       }
                                   }
                               }
                           ]
                       }
                       """
        });
        
        var servicePrincipalRole = new IamRole(this, "ServicePrincipalRole", new IamRoleConfig
        {
            Name = $"{env}-{service}-principal",
            AssumeRolePolicy = """
                               {
                                   "Version": "2012-10-17",
                                   "Statement": [
                                       {
                                           "Effect": "Allow",
                                           "Principal": {
                                               "Service": "tasks.apprunner.amazonaws.com"
                                           },
                                           "Action": "sts:AssumeRole"
                                       }
                                   ]
                               }
                               """
        });

        new IamRolePolicy(this, "ServicePrincipalPolicy", new IamRolePolicyConfig
        {
            Name = $"{env}-{service}-principal-policy",
            Role = servicePrincipalRole.Id,
            Policy = $$"""
                       {
                           "Version": "2012-10-17",
                           "Statement": [
                               {
                                   "Effect": "Allow",
                                   "Action": [
                                       "sns:Publish"
                                   ],
                                   "Resource": [
                                       "{{interactionDeferredTopic.Arn}}",
                                       "{{interactionReceivedTopic.Arn}}"
                                   ]
                               },
                               {
                                   "Effect": "Allow",
                                   "Action": [
                                       "sqs:ReceiveMessage",
                                       "sqs:DeleteMessage",
                                       "sqs:GetQueueAttributes",
                                       "sqs:ChangeMessageVisibility"
                                   ],
                                   "Resource": [
                                       "{{interactionDeferredQueue.Arn}}",
                                       "{{interactionReceivedQueue.Arn}}"
                                   ]
                               }
                           ]
                       }
                       """
        });
    }
}