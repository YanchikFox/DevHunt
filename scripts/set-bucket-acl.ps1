# Make avatars bucket publicly readable using ACL
# This allows browser to access avatar images without authentication

$bucketName = "avatars"

Write-Host "Setting public-read ACL for bucket: $bucketName"

try {
    # Get credentials from environment or use defaults
    $accessKey = $env:OBJECT_STORAGE_ACCESS_KEY
    if (-not $accessKey) {
        $accessKey = "devhunt_access_key_dev_123456789012"
    }

    $secretKey = $env:OBJECT_STORAGE_SECRET_KEY
    if (-not $secretKey) {
        $secretKey = "devhunt_secret_key_dev_1234567890123456"
    }

    Write-Host "Using AWS CLI to set bucket ACL..."
    
    # Use AWS CLI to set bucket ACL to public-read
    docker run --rm `
        --network devhunt_default `
        -e AWS_ACCESS_KEY_ID=$accessKey `
        -e AWS_SECRET_ACCESS_KEY=$secretKey `
        amazon/aws-cli `
        s3api put-bucket-acl `
        --bucket $bucketName `
        --acl public-read `
        --endpoint-url http://object-storage:8333

    Write-Host "`nBucket ACL applied successfully!"
    Write-Host "Bucket '$bucketName' is now publicly readable."
    
    Write-Host "`nTesting access to uploaded file..."
    $testUrl = "http://localhost:8333/avatars/01b5639f-68e5-4af2-89fa-437d5eea0246/245a8a48-fc66-4d83-9bca-abcc1bed3eee.svg"
    Write-Host "Test URL: $testUrl"
    
    $response = Invoke-WebRequest -Uri $testUrl -Method Head -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Success! File is publicly accessible" -ForegroundColor Green
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "`nNote: SeaweedFS might not support ACL commands."
    Write-Host "Alternative: We need to configure anonymous read access differently."
}
