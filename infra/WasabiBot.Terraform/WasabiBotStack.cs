using System.Text.Json;
using Amazon.ECR;
using Amazon.ECR.Model;
using Constructs;
using HashiCorp.Cdktf;
using HashiCorp.Cdktf.Providers.Aws.Provider;
using HashiCorp.Cdktf.Providers.Aws.SnsTopic;
using HashiCorp.Cdktf.Providers.Aws.SnsTopicSubscription;
using HashiCorp.Cdktf.Providers.Aws.SqsQueue;
using HashiCorp.Cdktf.Providers.Aws.SqsQueuePolicy;
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
            Policy = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                {
                    "Version", "2012-10-17"
                },
                {
                    "Statement", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "Effect", "Allow" },
                            { "Principal", new Dictionary<string, object>
                                {
                                    { "Service", "sns.amazonaws.com" }
                                }
                            },
                            { "Action", "sqs:SendMessage" },
                            { "Resource", interactionDeferredQueue.Arn },
                            { "Condition", new Dictionary<string, object>
                                {
                                    { "ArnEquals", new Dictionary<string, string>
                                        {
                                            { "aws:SourceArn", interactionDeferredTopic.Arn }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            })
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
            Policy = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                {
                    "Version", "2012-10-17"
                },
                {
                    "Statement", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "Effect", "Allow" },
                            { "Principal", new Dictionary<string, object>
                                {
                                    { "Service", "sns.amazonaws.com" }
                                }
                            },
                            { "Action", "sqs:SendMessage" },
                            { "Resource", interactionReceivedQueue.Arn },
                            { "Condition", new Dictionary<string, object>
                                {
                                    { "ArnEquals", new Dictionary<string, string>
                                        {
                                            { "aws:SourceArn", interactionReceivedTopic.Arn }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            })
        });
        
        // var latestDigest = GetLatestImageDigestAsync(service, env).Result;
        
        // Outputs
        // new TerraformOutput(this, "DeferredQueueUrl", new TerraformOutputConfig
        // {
        //     Value = interactionDeferredQueue.Url
        // });
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