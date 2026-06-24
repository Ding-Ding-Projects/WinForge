using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// AWS 服務／操作目錄 · A static fallback catalog of common AWS services, used when live
/// "aws help" enumeration is unavailable (e.g. before the CLI is installed) so the browser
/// still shows something useful. The live enumeration in <see cref="AwsCliService"/> always
/// takes precedence when the CLI is present.
/// </summary>
public static class AwsServiceCatalog
{
    /// <summary>常見服務（live 枚舉失敗時嘅後備）· Common services (fallback list).</summary>
    public static readonly string[] CommonServices =
    {
        "accessanalyzer", "account", "acm", "acm-pca", "amplify", "apigateway", "apigatewayv2",
        "appconfig", "appflow", "application-autoscaling", "appstream", "appsync", "athena",
        "autoscaling", "backup", "batch", "bedrock", "bedrock-runtime", "budgets", "cloud9",
        "cloudformation", "cloudfront", "cloudhsm", "cloudsearch", "cloudtrail", "cloudwatch",
        "codeartifact", "codebuild", "codecommit", "codedeploy", "codepipeline", "codestar",
        "cognito-identity", "cognito-idp", "comprehend", "config", "connect", "databrew",
        "datapipeline", "datasync", "dax", "detective", "devicefarm", "directconnect", "dlm",
        "dms", "docdb", "ds", "dynamodb", "dynamodbstreams", "ebs", "ec2", "ecr", "ecr-public",
        "ecs", "efs", "eks", "elasticache", "elasticbeanstalk", "elastictranscoder", "elb",
        "elbv2", "emr", "es", "events", "firehose", "fms", "forecast", "fsx", "gamelift",
        "glacier", "globalaccelerator", "glue", "greengrass", "guardduty", "health", "iam",
        "imagebuilder", "inspector", "inspector2", "iot", "iotanalytics", "kafka", "kendra",
        "kinesis", "kinesisanalytics", "kinesisvideo", "kms", "lakeformation", "lambda", "lex-models",
        "license-manager", "lightsail", "logs", "macie2", "mediaconnect", "mediaconvert", "medialive",
        "mediapackage", "mediastore", "mediatailor", "memorydb", "mgn", "mq", "neptune",
        "networkmanager", "opensearch", "opsworks", "organizations", "outposts", "personalize",
        "pinpoint", "polly", "pricing", "qldb", "quicksight", "ram", "rds", "rds-data",
        "redshift", "rekognition", "resource-groups", "resourcegroupstaggingapi", "robomaker",
        "route53", "route53domains", "route53resolver", "s3", "s3api", "s3control", "sagemaker",
        "secretsmanager", "securityhub", "serverlessrepo", "service-quotas", "servicecatalog",
        "servicediscovery", "ses", "sesv2", "shield", "signer", "sms", "snowball", "sns", "sqs",
        "ssm", "sso", "sso-admin", "stepfunctions", "storagegateway", "sts", "support", "swf",
        "synthetics", "textract", "timestream-query", "timestream-write", "transcribe", "transfer",
        "translate", "waf", "wafv2", "wellarchitected", "workdocs", "workmail", "workspaces", "xray",
    };
}
