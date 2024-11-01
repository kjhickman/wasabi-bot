using System.Text.Json;
using Constructs;
using HashiCorp.Cdktf;
using HashiCorp.Cdktf.Providers.Aws.EcsService;
using HashiCorp.Cdktf.Providers.Aws.EcsTaskDefinition;
using HashiCorp.Cdktf.Providers.Aws.IamRole;
using HashiCorp.Cdktf.Providers.Aws.IamRolePolicy;
using HashiCorp.Cdktf.Providers.Aws.Provider;
using HashiCorp.Cdktf.Providers.Aws.SnsTopic;
using HashiCorp.Cdktf.Providers.Aws.SnsTopicSubscription;
using HashiCorp.Cdktf.Providers.Aws.SqsQueue;
using HashiCorp.Cdktf.Providers.Aws.SqsQueuePolicy;
using WasabiBot.Terraform.Settings;

// ReSharper disable ObjectCreationAsStatement

namespace WasabiBot.Terraform.Stacks;

internal class WasabiBotStack : TerraformStack
{
    public WasabiBotStack(Construct scope, string id, EnvironmentVariables vars, WasabiBotSharedStack sharedStack) : base(scope, id)
    {
        var env = vars.ENVIRONMENT;
        const string region = "us-east-1";
        const string service = "wasabi-bot";
        
        AddDependency(sharedStack);

        // Create remote state data source to get shared stack outputs
        var sharedRemoteState = new DataTerraformRemoteStateS3(this, "SharedState", new DataTerraformRemoteStateS3Config
        {
            Bucket = "tfstate-b5f4b976dc5f",
            Key = $"{service}.shared.tfstate",
            Region = region,
        });

        var ecrRepositoryUrl = sharedRemoteState.Get("ecrRepoUrl").ToString();
        var ecsClusterArn = sharedRemoteState.Get("ecsClusterArn").ToString();


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

        var taskExecutionRole = new IamRole(this, "TaskExecutionRole", new IamRoleConfig
        {
            Name = $"{env}-{service}-execution-role",
            AssumeRolePolicy = """
                               {
                                   "Version": "2012-10-17",
                                   "Statement": [
                                       {
                                           "Action": "sts:AssumeRole",
                                           "Effect": "Allow",
                                           "Principal": {
                                               "Service": "ecs-tasks.amazonaws.com"
                                           }
                                       }
                                   ]
                               }
                               """
        });

        // Attach policy to task execution role
        new IamRolePolicy(this, "TaskExecutionRolePolicy", new IamRolePolicyConfig
        {
            Name = $"{env}-{service}-execution-policy",
            Role = taskExecutionRole.Id,
            Policy = """
                     {
                         "Version": "2012-10-17",
                         "Statement": [
                             {
                                 "Effect": "Allow",
                                 "Action": [
                                     "ecr:GetAuthorizationToken",
                                     "ecr:BatchCheckLayerAvailability",
                                     "ecr:GetDownloadUrlForLayer",
                                     "ecr:BatchGetImage",
                                     "logs:CreateLogStream",
                                     "logs:PutLogEvents"
                                 ],
                                 "Resource": "*"
                             }
                         ]
                     }
                     """
        });

        // Create task role with permissions to access SQS and SNS
        var taskRole = new IamRole(this, "TaskRole", new IamRoleConfig
        {
            Name = $"{env}-{service}-task-role",
            AssumeRolePolicy = """
                               {
                                   "Version": "2012-10-17",
                                   "Statement": [
                                       {
                                           "Action": "sts:AssumeRole",
                                           "Effect": "Allow",
                                           "Principal": {
                                               "Service": "ecs-tasks.amazonaws.com"
                                           }
                                       }
                                   ]
                               }
                               """
        });

        // Add SQS and SNS permissions to task role
        new IamRolePolicy(this, "TaskRolePolicy", new IamRolePolicyConfig
        {
            Name = $"{env}-{service}-task-policy",
            Role = taskRole.Id,
            Policy = $$"""
                       {
                           "Version": "2012-10-17",
                           "Statement": [
                               {
                                   "Effect": "Allow",
                                   "Action": [
                                       "sqs:ReceiveMessage",
                                       "sqs:DeleteMessage",
                                       "sqs:GetQueueAttributes",
                                       "sns:Publish"
                                   ],
                                   "Resource": [
                                       "{{interactionDeferredQueue.Arn}}",
                                       "{{interactionReceivedQueue.Arn}}",
                                       "{{interactionDeferredTopic.Arn}}",
                                       "{{interactionReceivedTopic.Arn}}"
                                   ]
                               }
                           ]
                       }
                       """
        });

        // Create task definition
        var taskDefinition = new EcsTaskDefinition(this, "TaskDefinition", new EcsTaskDefinitionConfig
        {
            Family = $"{env}-{service}",
            RequiresCompatibilities = ["FARGATE"],
            NetworkMode = "awsvpc",
            Cpu = "256",
            Memory = "512",
            ExecutionRoleArn = taskExecutionRole.Arn,
            TaskRoleArn = taskRole.Arn,
            ContainerDefinitions = $$"""
                                     [
                                         {
                                             "name": "{{service}}",
                                             "image": "{{ecrRepositoryUrl}}:latest",
                                             "essential": true,
                                             "logConfiguration": {
                                                 "logDriver": "awslogs",
                                                 "options": {
                                                     "awslogs-group": "/ecs/{{env}}-{{service}}",
                                                     "awslogs-region": "{{region}}",
                                                     "awslogs-stream-prefix": "ecs"
                                                 }
                                             },
                                             "environment": [
                                                 {
                                                     "name": "ENVIRONMENT",
                                                     "value": "{{env}}"
                                                 }
                                             ]
                                         }
                                     ]
                                     """
        });

        // Create ECS Service
        new EcsService(this, "Service", new EcsServiceConfig
        {
            Name = $"{env}-{service}",
            Cluster = ecrRepositoryUrl,
            TaskDefinition = taskDefinition.Arn,
            DesiredCount = 1,
            LaunchType = "FARGATE",
            CapacityProviderStrategy = new[] { new EcsServiceCapacityProviderStrategy { CapacityProvider = "FARGATE_SPOT", Weight = 1 } },
            NetworkConfiguration = new EcsServiceNetworkConfiguration
            {
                AssignPublicIp = true,
                SecurityGroups =
                [
                    ""
                ],
                Subnets =
                [
                    ""
                ]
            }
        });
    }
}