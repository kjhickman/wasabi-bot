using Constructs;
using HashiCorp.Cdktf;
using HashiCorp.Cdktf.Providers.Aws.EcrRepository;
using HashiCorp.Cdktf.Providers.Aws.EcsCluster;
using HashiCorp.Cdktf.Providers.Aws.Provider;

namespace WasabiBot.Terraform.Stacks;

public class WasabiBotSharedStack : TerraformStack
{
    public TerraformOutput EcrRepositoryUrl { get; private set; }
    public TerraformOutput EcsClusterArn { get; private set; }
    
    public WasabiBotSharedStack(Construct scope, string id) : base(scope, id)
    {
        const string region = "us-east-1";
        const string service = "wasabi-bot";
        
        var defaultTags = new AwsProviderDefaultTags
        {
            Tags = new Dictionary<string, string>
            {
                { "Service", service }
            }
        };
        
        new AwsProvider(this, "Aws", new AwsProviderConfig
        {
            Region = "us-east-1",
            DefaultTags = new[] { defaultTags }
        });
        
        new S3Backend(this, new S3BackendConfig
        {
            Bucket = "tfstate-b5f4b976dc5f",
            Key = $"{service}.shared.tfstate",
            Region = region,
        });
        
        var ecrRepo = new EcrRepository(this, "WasabiBotSharedEcrRepo", new EcrRepositoryConfig
        {
            Name = service,
            ImageTagMutability = "MUTABLE",
            ImageScanningConfiguration = new EcrRepositoryImageScanningConfiguration
            {
                ScanOnPush = true
            }
        });
        
        var ecsCluster = new EcsCluster(this, "WasabiBotSharedEcsCluster", new EcsClusterConfig
        {
            Name = service,
        });
        
        EcrRepositoryUrl = new TerraformOutput(this, "ecrRepoUrl", new TerraformOutputConfig
        {
            Value = ecrRepo.RepositoryUrl,
            Description = "ECR Repository URL"
        });
        
        EcsClusterArn = new TerraformOutput(this, "ecsClusterArn", new TerraformOutputConfig
        {
            Value = ecsCluster.Arn,
            Description = "ECS Cluster ARN"
        });
    }
}