using Constructs;
using HashiCorp.Cdktf;
using HashiCorp.Cdktf.Providers.Aws.DataAwsCallerIdentity;
using HashiCorp.Cdktf.Providers.Aws.IamRole;
using HashiCorp.Cdktf.Providers.Aws.IamRolePolicy;
using HashiCorp.Cdktf.Providers.Aws.Provider;
using HashiCorp.Cdktf.Providers.Aws.SqsQueue;

// ReSharper disable ObjectCreationAsStatement

namespace WasabiBot.Terraform;

internal class WasabiBotStack : TerraformStack
{   
    public WasabiBotStack(Construct scope, string id, string environment) : base(scope, id)
    {
        const string region = "us-east-1";
        const string service = "wasabi-bot";

        var defaultTags = new AwsProviderDefaultTags
        {
            Tags = new Dictionary<string, string>
            {
                { "Environment", environment },
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
            Key = $"{service}.{environment}.tfstate",
            Region = region,
            // TODO: add DynamoDB locking
        });
        
        // TODO: create reusable module for new queues
        var interactionDeferredErrorQueue = new SqsQueue(this, "InteractionDeferredErrorQueue", new SqsQueueConfig
        {
            Name = $"{environment}-{service}-interaction-deferred-error",
            MessageRetentionSeconds = 750,
            VisibilityTimeoutSeconds = 30
        });
        
        var interactionDeferredQueue = new SqsQueue(this, "InteractionDeferredQueue", new SqsQueueConfig
        {
            Name = $"{environment}-{service}-interaction-deferred",
            VisibilityTimeoutSeconds = 30,
            RedrivePolicy = $"{{\"deadLetterTargetArn\":\"{interactionDeferredErrorQueue.Arn}\", \"maxReceiveCount\": 3}}"
        });
        
        var interactionReceivedErrorQueue = new SqsQueue(this, "InteractionReceivedErrorQueue", new SqsQueueConfig
        {
            Name = $"{environment}-{service}-interaction-received-error",
            MessageRetentionSeconds = 750,
            VisibilityTimeoutSeconds = 30
        });
        
        var interactionReceivedQueue = new SqsQueue(this, "InteractionReceivedQueue", new SqsQueueConfig
        {
            Name = $"{environment}-{service}-interaction-received",
            VisibilityTimeoutSeconds = 30,
            RedrivePolicy = $"{{\"deadLetterTargetArn\":\"{interactionReceivedErrorQueue.Arn}\", \"maxReceiveCount\": 3}}"
        });
        
        var callerIdentity = new DataAwsCallerIdentity(this, "current");
        var wasabiBotWebFlyRole = new IamRole(this, "WasabiBotWebFlyRole", new IamRoleConfig
        {
            Name = $"{environment}-{service}-web-fly-role",
            AssumeRolePolicy = $$"""
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Principal": {
                                "Federated": "arn:aws:iam::{{callerIdentity.AccountId}}:oidc-provider/oidc.fly.io/wasabi-bot"
                            },
                            "Action": "sts:AssumeRoleWithWebIdentity",
                            "Condition": {
                                "StringEquals": {
                                    "oidc.fly.io/wasabi-bot:aud": "sts.amazonaws.com"
                                },
                                "StringLike": {
                                    "oidc.fly.io/wasabi-bot:sub": "wasabi-bot:{{environment}}-wasabi-bot-web:*"
                                }
                            }
                        }
                    ]
                }
                """
        });

        new IamRolePolicy(this, "WasabiBotWebFlyRolePolicy", new IamRolePolicyConfig
        {
            Name = $"{environment}-{service}-web-fly-policy",
            Role = wasabiBotWebFlyRole.Id,
            Policy = $$"""
                       {
                           "Version": "2012-10-17",
                           "Statement": [
                               {
                                   "Effect": "Allow",
                                   "Action": [
                                       "sqs:SetQueueAttributes",
                                       "sqs:ReceiveMessage",
                                       "sqs:DeleteMessage",
                                       "sqs:SendMessage",
                                       "sqs:GetQueueUrl",
                                       "sqs:GetQueueAttributes",
                                       "sqs:ChangeMessageVisibility",
                                       "sqs:PurgeQueue"
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