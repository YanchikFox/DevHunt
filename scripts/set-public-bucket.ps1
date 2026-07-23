# Make avatars bucket publicly readable
# This allows browser to access avatar images without authentication

$bucketName = "avatars"
$endpoint = "http://localhost:8333"

Write-Host "Setting public read policy for bucket: $bucketName"

# AWS S3 bucket policy for public read access
$policy = @"
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PublicReadGetObject",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::$bucketName/*"
    }
  ]
}
"@

# Save policy to temp file
$tempFile = [System.IO.Path]::GetTempFileName()
Set-Content -Path $tempFile -Value $policy -Encoding UTF8

Write-Host "Policy file: $tempFile"
Write-Host "Policy content:"
Write-Host $policy

try {
    # Get credentials from environment and require explicit values
    $accessKey = $env:OBJECT_STORAGE_ACCESS_KEY
    if (-not $accessKey) {
        throw "OBJECT_STORAGE_ACCESS_KEY is required. Set it before running this script."
    }

    $secretKey = $env:OBJECT_STORAGE_SECRET_KEY
    if (-not $secretKey) {
        throw "OBJECT_STORAGE_SECRET_KEY is required. Set it before running this script."
    }

    # Set AWS credentials for awscli
    $env:AWS_ACCESS_KEY_ID = $accessKey
    $env:AWS_SECRET_ACCESS_KEY = $secretKey

    Write-Host "`nApplying bucket policy using Docker container with AWS CLI..."
    
    # Use AWS CLI in a container to set the bucket policy
    docker run --rm `
        --network devhunt_default `
        -e AWS_ACCESS_KEY_ID=$accessKey `
        -e AWS_SECRET_ACCESS_KEY=$secretKey `
        -v "${tempFile}:/tmp/policy.json" `
        amazon/aws-cli `
        s3api put-bucket-policy `
        --bucket $bucketName `
        --policy file:///tmp/policy.json `
        --endpoint-url http://object-storage:8333

    Write-Host "`nBucket policy applied successfully!"
    Write-Host "Bucket '$bucketName' is now publicly readable."
    
} catch {
    Write-Host "Failed to apply bucket policy: $_" -ForegroundColor Red
} finally {
    # Cleanup temp file
    if (Test-Path $tempFile) {
        Remove-Item $tempFile -Force
    }
}

Write-Host "`nTo verify, try accessing: $endpoint/$bucketName/<any-object-key>"
