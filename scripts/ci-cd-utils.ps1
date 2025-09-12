# CI/CD Utility Scripts for WhoAndWhat API
# These scripts help automate CI/CD pipeline tasks

param(
    [Parameter(Mandatory = $false)]
    [string]$Action = "help"
)

# Function to display help
function Show-Help {
    Write-Host "WhoAndWhat API CI/CD Utility Scripts" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\ci-cd-utils.ps1 -Action <action>" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Available Actions:" -ForegroundColor Cyan
    Write-Host "  validate-build    - Validate build configuration and dependencies"
    Write-Host "  run-tests        - Run all test suites with coverage"
    Write-Host "  security-check   - Run security validation checks"
    Write-Host "  validate-docker  - Validate Docker configuration"
    Write-Host "  coverage-report  - Generate detailed code coverage report"
    Write-Host "  deploy-check     - Validate deployment readiness"
    Write-Host "  help             - Show this help message"
}

# Function to validate build configuration
function Test-BuildConfiguration {
    Write-Host "Validating Build Configuration..." -ForegroundColor Yellow
    
    # Check if solution file exists
    $solutionFile = Get-ChildItem -Path "." -Filter "*.sln" -Recurse | Select-Object -First 1
    if (-not $solutionFile) {
        Write-Error "Solution file not found!"
        return $false
    }
    
    # Check required files
    $requiredFiles = @(
        "Dockerfile",
        "docker-compose.yml",
        "azure-pipelines.yml",
        ".runsettings"
    )
    
    $missing = @()
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path $file)) {
            $missing += $file
        }
    }
    
    if ($missing.Count -gt 0) {
        Write-Warning "Missing required files: $($missing -join ', ')"
        return $false
    }
    
    # Check .NET version
    try {
        $dotnetVersion = dotnet --version
        Write-Host "✓ .NET Version: $dotnetVersion" -ForegroundColor Green
    }
    catch {
        Write-Error "✗ .NET SDK not found or not accessible"
        return $false
    }
    
    # Restore packages
    Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "✗ Package restore failed"
        return $false
    }
    
    Write-Host "✓ Build configuration validation passed" -ForegroundColor Green
    return $true
}

# Function to run comprehensive tests
function Invoke-TestSuite {
    Write-Host "Running Comprehensive Test Suite..." -ForegroundColor Yellow
    
    # Build solution first
    Write-Host "Building solution..." -ForegroundColor Cyan
    dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "✗ Build failed"
        return $false
    }
    
    # Run unit tests with coverage
    Write-Host "Running unit tests with coverage..." -ForegroundColor Cyan
    dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory "./TestResults" --logger trx
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Some tests failed - check test results"
    }
    
    # Run architecture tests
    Write-Host "Running architecture validation tests..." -ForegroundColor Cyan
    dotnet test tests/WhoAndWhat.Architecture.Tests/ --configuration Release --no-build --logger trx
    if ($LASTEXITCODE -ne 0) {
        Write-Error "✗ Architecture tests failed"
        return $false
    }
    
    # Run CI/CD validation tests
    Write-Host "Running CI/CD validation tests..." -ForegroundColor Cyan
    dotnet test tests/WhoAndWhat.CI.Tests/ --configuration Release --no-build --logger trx
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "CI/CD validation tests failed - review configuration"
    }
    
    Write-Host "✓ Test suite execution completed" -ForegroundColor Green
    return $true
}

# Function to run security checks
function Invoke-SecurityCheck {
    Write-Host "Running Security Validation..." -ForegroundColor Yellow
    
    # Check for hardcoded secrets in configuration files
    Write-Host "Checking for hardcoded secrets..." -ForegroundColor Cyan
    $configFiles = Get-ChildItem -Path "." -Filter "appsettings*.json" -Recurse
    $secretPatterns = @("password", "secret", "key=", "token=")
    
    $foundSecrets = $false
    foreach ($file in $configFiles) {
        $content = Get-Content $file.FullName -Raw
        foreach ($pattern in $secretPatterns) {
            if ($content -match $pattern) {
                Write-Warning "Potential secret found in $($file.Name): $pattern"
                $foundSecrets = $true
            }
        }
    }
    
    if (-not $foundSecrets) {
        Write-Host "✓ No hardcoded secrets found in configuration" -ForegroundColor Green
    }
    
    # Check Docker security
    Write-Host "Validating Docker security configuration..." -ForegroundColor Cyan
    if (Test-Path "Dockerfile") {
        $dockerContent = Get-Content "Dockerfile" -Raw
        if ($dockerContent -match "USER") {
            Write-Host "✓ Dockerfile uses non-root user" -ForegroundColor Green
        }
        else {
            Write-Warning "Dockerfile should specify a non-root USER"
        }
        
        if ($dockerContent -match "HEALTHCHECK") {
            Write-Host "✓ Dockerfile includes health check" -ForegroundColor Green
        }
        else {
            Write-Warning "Dockerfile should include HEALTHCHECK instruction"
        }
    }
    
    Write-Host "✓ Security validation completed" -ForegroundColor Green
    return $true
}

# Function to validate Docker configuration
function Test-DockerConfiguration {
    Write-Host "Validating Docker Configuration..." -ForegroundColor Yellow
    
    # Check if Docker is available
    try {
        docker --version | Out-Null
        Write-Host "✓ Docker is available" -ForegroundColor Green
    }
    catch {
        Write-Error "✗ Docker not found - install Docker to run container tests"
        return $false
    }
    
    # Test Docker build
    Write-Host "Testing Docker build..." -ForegroundColor Cyan
    docker build -t whoandwhat-api-test .
    if ($LASTEXITCODE -ne 0) {
        Write-Error "✗ Docker build failed"
        return $false
    }
    
    Write-Host "✓ Docker build successful" -ForegroundColor Green
    
    # Run Docker tests
    Write-Host "Running Docker integration tests..." -ForegroundColor Cyan
    dotnet test tests/WhoAndWhat.Docker.Tests/ --configuration Release --logger trx
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Docker tests failed - check container configuration"
    }
    
    # Clean up test image
    docker rmi whoandwhat-api-test -f | Out-Null
    
    return $true
}

# Function to generate coverage report
function New-CoverageReport {
    Write-Host "Generating Code Coverage Report..." -ForegroundColor Yellow
    
    # Run tests with coverage
    dotnet test --collect:"XPlat Code Coverage" --results-directory "./TestResults"
    
    # Find coverage files
    $coverageFiles = Get-ChildItem -Path "./TestResults" -Filter "coverage.cobertura.xml" -Recurse
    
    if ($coverageFiles.Count -eq 0) {
        Write-Warning "No coverage files found"
        return $false
    }
    
    Write-Host "Coverage files generated:" -ForegroundColor Green
    foreach ($file in $coverageFiles) {
        Write-Host "  $($file.FullName)" -ForegroundColor Cyan
    }
    
    Write-Host "✓ Coverage report generation completed" -ForegroundColor Green
    return $true
}

# Function to validate deployment readiness
function Test-DeploymentReadiness {
    Write-Host "Validating Deployment Readiness..." -ForegroundColor Yellow
    
    $checks = @()
    
    # Check 1: All tests pass
    Write-Host "Checking test status..." -ForegroundColor Cyan
    dotnet test --configuration Release --no-build --logger trx > $null 2>&1
    $checks += @{ Name = "All tests pass"; Passed = ($LASTEXITCODE -eq 0) }
    
    # Check 2: Docker build succeeds
    Write-Host "Checking Docker build..." -ForegroundColor Cyan
    docker build -t whoandwhat-api-deploy-test . > $null 2>&1
    $dockerBuildSuccess = ($LASTEXITCODE -eq 0)
    $checks += @{ Name = "Docker build succeeds"; Passed = $dockerBuildSuccess }
    
    if ($dockerBuildSuccess) {
        docker rmi whoandwhat-api-deploy-test -f > $null 2>&1
    }
    
    # Check 3: Required environment variables documented
    $envFiles = @(".env.example", ".env.docker")
    $envFilesExist = $envFiles | ForEach-Object { Test-Path $_ } | Where-Object { $_ -eq $true }
    $checks += @{ Name = "Environment files exist"; Passed = ($envFilesExist.Count -gt 0) }
    
    # Check 4: Security configuration valid
    $dockerContent = Get-Content "Dockerfile" -Raw -ErrorAction SilentlyContinue
    $hasUser = $dockerContent -match "USER"
    $checks += @{ Name = "Docker security (non-root user)"; Passed = $hasUser }
    
    # Display results
    Write-Host "`nDeployment Readiness Check Results:" -ForegroundColor Yellow
    Write-Host "===================================" -ForegroundColor Yellow
    
    $allPassed = $true
    foreach ($check in $checks) {
        $status = if ($check.Passed) { "✓" } else { "✗"; $allPassed = $false }
        $color = if ($check.Passed) { "Green" } else { "Red" }
        Write-Host "$status $($check.Name)" -ForegroundColor $color
    }
    
    if ($allPassed) {
        Write-Host "`n✓ Deployment readiness validation passed" -ForegroundColor Green
    }
    else {
        Write-Host "`n✗ Deployment readiness validation failed" -ForegroundColor Red
    }
    
    return $allPassed
}

# Main execution logic
switch ($Action.ToLower()) {
    "validate-build" { Test-BuildConfiguration }
    "run-tests" { Invoke-TestSuite }
    "security-check" { Invoke-SecurityCheck }
    "validate-docker" { Test-DockerConfiguration }
    "coverage-report" { New-CoverageReport }
    "deploy-check" { Test-DeploymentReadiness }
    "help" { Show-Help }
    default { 
        Write-Host "Unknown action: $Action" -ForegroundColor Red
        Show-Help 
    }
}