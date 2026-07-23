$config = @'
{
  "identities": [
    {
      "name": "devhunt",
      "credentials": [
        {
          "accessKey": "devhunt_access_key_dev_123456789012",
          "secretKey": "devhunt_secret_key_dev_1234567890123456"
        }
      ],
      "actions": ["Admin","Read","List","Tagging","Write"]
    }
  ]
}
'@

$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"

$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"s3.json`"",
    "Content-Type: application/json$LF",
    $config,
    "--$boundary--$LF"
) -join $LF

try {
    $response = Invoke-WebRequest `
        -Uri "http://localhost:8888/etc/seaweedfs/s3.json" `
        -Method POST `
        -Body $bodyLines `
        -ContentType "multipart/form-data; boundary=$boundary"
    
    Write-Host "✓ S3 IAM config uploaded successfully!" -ForegroundColor Green
    Write-Host "Response: $($response.StatusCode) $($response.StatusDescription)"
} catch {
    Write-Host "✗ Failed to upload S3 IAM config" -ForegroundColor Red
    Write-Host "Error: $_"
}
