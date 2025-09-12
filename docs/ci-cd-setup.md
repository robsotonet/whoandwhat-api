# CI/CD Pipeline Setup - WhoAndWhat API

## Overview

This document describes the CI/CD pipeline foundation implemented for the WhoAndWhat API project, including build, test, security, and deployment stages.

## Pipeline Architecture

### Azure DevOps Pipeline (`azure-pipelines.yml`)

The pipeline implements a comprehensive CI/CD workflow with the following stages:

#### 1. **Build Stage**
- ✅ .NET 9.0 SDK setup
- ✅ Package restoration and dependency management
- ✅ Solution build with Release configuration
- ✅ Unit test execution with coverage collection
- ✅ Architecture validation tests
- ✅ Docker image build and registry push
- ✅ Multi-format coverage reporting (Cobertura, OpenCover)

#### 2. **Security Stage**
- ✅ SonarCloud static analysis integration
- ✅ OWASP Dependency Check for vulnerability scanning
- ✅ Code quality and security metrics collection
- ✅ Security scan results publishing

#### 3. **Integration Testing Stage**
- ✅ PostgreSQL and Redis service containers
- ✅ Docker integration tests
- ✅ API integration tests with database connectivity
- ✅ End-to-end validation testing

#### 4. **Staging Deployment Stage**
- ✅ Azure Container Instances deployment
- ✅ Environment-specific configuration management
- ✅ Smoke tests execution against staging environment
- ✅ Automated rollback on failure

#### 5. **Production Deployment Stage**
- ✅ Manual approval gate integration
- ✅ Blue-green deployment strategy support
- ✅ Production health checks validation
- ✅ Monitoring and alerting setup

## Test Coverage Configuration

### Test Run Settings (`.runsettings`)
```xml
<Configuration>
  <Format>cobertura,opencover</Format>
  <Exclude>[*Test*]*,[*Tests*]*</Exclude>
  <ExcludeByFile>**/Migrations/*.cs</ExcludeFile>
  <SkipAutoProps>true</SkipAutoProps>
</Configuration>
```

### Coverage Requirements
- **Minimum Coverage**: 80% line coverage
- **Exclusions**: Test projects, migrations, auto-generated code
- **Formats**: Cobertura (Azure DevOps), OpenCover (SonarCloud)

## CI/CD Validation Tests

### Pipeline Validation (`WhoAndWhat.CI.Tests`)

**Purpose**: Automated validation of CI/CD pipeline components and deployment readiness.

**Test Categories**:
1. **Pipeline Validation Tests**
   - Application startup validation
   - OpenAPI specification integrity
   - Environment variable configuration
   - Project structure validation
   - Build artifact management

2. **Security Validation Tests**
   - Hardcoded secrets detection
   - Docker security configuration
   - Dependency vulnerability checks
   - Assembly signing validation
   - Security headers verification

3. **Deployment Validation Tests**
   - Application performance benchmarks
   - Concurrent request handling
   - Environment-specific configurations
   - Database migration automation
   - Resource requirement documentation
   - Monitoring and alerting setup

## Utility Scripts

### PowerShell CI/CD Utilities (`scripts/ci-cd-utils.ps1`)

**Available Commands**:
```powershell
# Validate build configuration
.\ci-cd-utils.ps1 -Action validate-build

# Run comprehensive test suite
.\ci-cd-utils.ps1 -Action run-tests

# Execute security validation
.\ci-cd-utils.ps1 -Action security-check

# Validate Docker configuration
.\ci-cd-utils.ps1 -Action validate-docker

# Generate coverage reports
.\ci-cd-utils.ps1 -Action coverage-report

# Check deployment readiness
.\ci-cd-utils.ps1 -Action deploy-check
```

## Environment Configuration

### Required Service Connections
1. **Azure Service Connection**: For container deployment
2. **Container Registry Connection**: For Docker image management
3. **SonarCloud Connection**: For static analysis
4. **Key Vault Connection**: For secrets management

### Environment Variables

#### Staging Environment
```yaml
ASPNETCORE_ENVIRONMENT: Staging
ConnectionStrings__DefaultConnection: $(StagingDbConnectionString)
JWT__SecretKey: $(StagingJwtSecret)
```

#### Production Environment
```yaml
ASPNETCORE_ENVIRONMENT: Production
ConnectionStrings__DefaultConnection: $(ProductionDbConnectionString)
JWT__SecretKey: $(ProductionJwtSecret)
```

### Secret Management
- **Development**: Local environment variables, `.env.docker`
- **Staging/Production**: Azure Key Vault integration
- **CI/CD Variables**: Azure DevOps Variable Groups (secured)

## Deployment Strategy

### Container Deployment
- **Registry**: Azure Container Registry
- **Orchestration**: Azure Container Instances (staging/production)
- **Scaling**: Manual scaling configuration
- **Health Checks**: Built-in container health monitoring

### Database Migrations
- **Strategy**: Automated migration during deployment
- **Rollback**: Database backup before migration
- **Validation**: Post-migration integrity checks

## Monitoring and Observability

### Application Insights Integration
- Performance metrics collection
- Error tracking and alerting  
- Custom telemetry and logging
- Availability monitoring

### Health Checks
- Database connectivity validation
- External service dependency checks
- Application startup verification
- Resource utilization monitoring

## Security Considerations

### Build Security
- **Signed Containers**: Docker image signing and verification
- **Vulnerability Scanning**: Automated dependency and container scanning
- **Secrets Management**: No hardcoded secrets in source code
- **Access Control**: Role-based access to pipeline resources

### Runtime Security
- **Non-root Containers**: Security-hardened container configuration
- **Network Security**: Restricted container network access
- **SSL/TLS**: Enforced HTTPS communication
- **Authentication**: JWT-based API authentication

## Getting Started

### Prerequisites
1. Azure DevOps project with appropriate service connections
2. Azure subscription with Container Registry and Container Instances
3. SonarCloud organization setup
4. Docker installed locally for development

### Pipeline Setup
1. Import `azure-pipelines.yml` into Azure DevOps
2. Configure required service connections
3. Set up environment-specific variable groups
4. Configure approval gates for production deployment
5. Run initial pipeline to validate configuration

### Local Development
```bash
# Run CI/CD validation locally
dotnet test tests/WhoAndWhat.CI.Tests/

# Execute utility scripts
pwsh scripts/ci-cd-utils.ps1 -Action deploy-check

# Build Docker image locally
docker build -t whoandwhat-api:local .
```

## Continuous Improvement

### Pipeline Optimization
- **Parallel Execution**: Independent stage parallelization
- **Cache Management**: NuGet package and Docker layer caching
- **Build Optimization**: Incremental builds and selective testing
- **Resource Scaling**: Dynamic agent scaling based on load

### Quality Gates
- **Code Coverage**: Minimum 80% coverage requirement
- **Security Scan**: Zero high-severity vulnerabilities
- **Performance**: Startup time under 30 seconds
- **Architecture**: Clean Architecture compliance validation

## Troubleshooting

### Common Issues
1. **Test Failures**: Check CI/CD validation test output for configuration gaps
2. **Docker Build Issues**: Validate Dockerfile and .dockerignore configuration
3. **Security Scan Failures**: Review dependency versions and security configurations
4. **Deployment Failures**: Check service connection and environment variable configuration

### Support Resources
- Azure DevOps pipeline documentation
- Docker best practices guide
- SonarCloud integration guide
- Azure Container Instances deployment guide

---

**Status**: ✅ CI/CD Pipeline Foundation Complete
**Next Steps**: Implement monitoring dashboards and production deployment validation