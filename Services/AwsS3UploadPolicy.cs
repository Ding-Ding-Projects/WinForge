namespace WinForge.Services;

/// <summary>Pure request policy for fail-closed S3 object uploads.</summary>
public static class AwsS3UploadPolicy
{
    public static string? IfNoneMatch(bool overwrite) => overwrite ? null : "*";
}
