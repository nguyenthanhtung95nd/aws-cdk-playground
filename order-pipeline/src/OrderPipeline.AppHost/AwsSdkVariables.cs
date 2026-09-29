namespace OrderPipeline.AppHost;

/// <summary>
/// Environment variables the AWS SDK reads. Nothing in this solution reads them.
/// </summary>
internal static class AwsSdkVariables
{
    public const string Region = "AWS_REGION";
    public const string AccessKeyId = "AWS_ACCESS_KEY_ID";
    public const string SecretAccessKey = "AWS_SECRET_ACCESS_KEY";

    // DynamoDB Local ignores credentials, but the SDK refuses to sign without one.
    public const string LocalCredential = "local";
}
