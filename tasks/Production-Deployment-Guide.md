# WhoAndWhat API - Production Deployment Guide

**Version**: 1.0  
**Date**: January 2025  
**Status**: Phase 2 Authentication System Ready for Production

## Overview

This guide provides comprehensive instructions for deploying the WhoAndWhat API authentication system to production environments. The system is production-ready with enterprise-grade security features.

## Prerequisites

### ✅ Completed Components
- **Authentication System**: JWT with refresh token rotation
- **Security Middleware**: DDoS protection, rate limiting, security headers
- **OAuth Integration**: Google, Facebook, Apple providers configured
- **Database Schema**: PostgreSQL with Entity Framework migrations
- **Monitoring**: Application Insights integration ready

### 📋 Production Requirements Checklist

## 1. Environment Configuration

### 1.1 Azure Key Vault Setup

**Required Keys**:
```json
{
  "JWT--SecretKey": "256-bit-cryptographically-secure-key",
  "JWT--Issuer": "https://api.whoandwhat.com",
  "JWT--Audience": "https://whoandwhat.com",
  "Database--DefaultConnection": "postgresql-connection-string",
  "SMTP--Host": "smtp-server-host",
  "SMTP--Username": "smtp-username",
  "SMTP--Password": "smtp-password"
}
```

**Configuration**:
```bash
# Azure Key Vault endpoint configuration
"KeyVault:Endpoint": "https://whoandwhat-prod-kv.vault.azure.net/"
```

### 1.2 Database Configuration

**PostgreSQL Setup**:
```json
{
  "Database": {
    "DefaultConnection": "Host=prod-db-server;Database=WhoAndWhat;Username=api_user;Password=[from-keyvault]",
    "CommandTimeout": 30,
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false
  }
}
```

**Migration Commands**:
```bash
# Apply database migrations
dotnet ef database update --project src/WhoAndWhat.Infrastructure/ --startup-project src/WhoAndWhat.API/

# Verify migration status
dotnet ef migrations list --project src/WhoAndWhat.Infrastructure/ --startup-project src/WhoAndWhat.API/
```

## 2. OAuth Provider Configuration

### 2.1 Google OAuth 2.0 Setup

**Google Cloud Console Configuration**:
1. Create new project in Google Cloud Console
2. Enable Google+ API and Google OAuth2 API
3. Create OAuth 2.0 credentials
4. Configure authorized redirect URIs

**Required Redirect URIs**:
```
https://api.whoandwhat.com/api/v1/oauth/google/callback
https://whoandwhat.com/auth/google/callback
```

**Configuration**:
```json
{
  "OAuth": {
    "Google": {
      "ClientId": "[from-google-cloud-console]",
      "ClientSecret": "[store-in-azure-keyvault]",
      "CallbackPath": "/api/v1/oauth/google/callback"
    }
  }
}
```

### 2.2 Facebook OAuth Setup

**Facebook Developers Configuration**:
1. Create new app in Facebook Developers
2. Add Facebook Login product
3. Configure Valid OAuth Redirect URIs

**Required Redirect URIs**:
```
https://api.whoandwhat.com/api/v1/oauth/facebook/callback
https://whoandwhat.com/auth/facebook/callback
```

**Configuration**:
```json
{
  "OAuth": {
    "Facebook": {
      "AppId": "[from-facebook-developers]",
      "AppSecret": "[store-in-azure-keyvault]",
      "CallbackPath": "/api/v1/oauth/facebook/callback"
    }
  }
}
```

### 2.3 Apple Sign-In Setup

**Apple Developer Configuration**:
1. Create new App ID in Apple Developer Account
2. Enable Sign In with Apple capability
3. Create Service ID for web authentication
4. Generate private key for JWT signing

**Required Redirect URIs**:
```
https://api.whoandwhat.com/api/v1/oauth/apple/callback
```

**Configuration**:
```json
{
  "OAuth": {
    "Apple": {
      "ClientId": "[service-id-from-apple]",
      "TeamId": "[apple-team-id]",
      "KeyId": "[private-key-id]",
      "PrivateKey": "[store-in-azure-keyvault]",
      "CallbackPath": "/api/v1/oauth/apple/callback"
    }
  }
}
```

## 3. Email Service Configuration

### 3.1 SMTP Configuration

**Recommended Providers**:
- **SendGrid**: Enterprise email service with API support
- **Amazon SES**: AWS Simple Email Service
- **Azure Communication Services**: Microsoft email service

**Configuration**:
```json
{
  "SMTP": {
    "Host": "smtp.sendgrid.net",
    "Port": 587,
    "Username": "apikey",
    "Password": "[sendgrid-api-key-from-keyvault]",
    "EnableSSL": true,
    "FromEmail": "noreply@whoandwhat.com",
    "FromName": "WhoAndWhat Support"
  }
}
```

### 3.2 Email Templates

**Required Templates**:
1. **Welcome Email**: User registration confirmation
2. **Password Reset**: Secure password reset instructions
3. **Email Verification**: Account email verification
4. **Password Changed**: Security notification

**Template Configuration**:
```json
{
  "EmailTemplates": {
    "Welcome": "templates/welcome-email.html",
    "PasswordReset": "templates/password-reset.html",
    "EmailVerification": "templates/email-verification.html",
    "PasswordChanged": "templates/password-changed.html"
  }
}
```

## 4. Security Configuration

### 4.1 JWT Settings

**Production JWT Configuration**:
```json
{
  "JWT": {
    "SecretKey": "[256-bit-key-from-azure-keyvault]",
    "Issuer": "https://api.whoandwhat.com",
    "Audience": "https://whoandwhat.com",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7,
    "ValidateIssuerSigningKey": true,
    "ValidateIssuer": true,
    "ValidateAudience": true,
    "ValidateLifetime": true,
    "RequireExpirationTime": true,
    "ClockSkewMinutes": 5
  }
}
```

### 4.2 Security Headers Configuration

**Production Security Headers**:
```json
{
  "SecurityHeaders": {
    "Enabled": true,
    "RemoveServerHeader": true,
    "XContentTypeOptions": "nosniff",
    "XFrameOptions": "DENY",
    "ReferrerPolicy": "strict-origin-when-cross-origin",
    "HSTS": {
      "Enabled": true,
      "MaxAge": 31536000,
      "IncludeSubDomains": true,
      "Preload": true
    },
    "ContentSecurityPolicy": {
      "Enabled": true,
      "DefaultSrc": "'self'",
      "ScriptSrc": "'self' 'unsafe-inline'",
      "StyleSrc": "'self' 'unsafe-inline'",
      "ImgSrc": "'self' data: https:",
      "ConnectSrc": "'self'",
      "FontSrc": "'self'",
      "ObjectSrc": "'none'",
      "MediaSrc": "'self'",
      "FrameAncestors": "'none'"
    }
  }
}
```

### 4.3 Rate Limiting Configuration

**Production Rate Limiting**:
```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/v1/auth/login",
        "Period": "1m",
        "Limit": 5
      },
      {
        "Endpoint": "POST:/api/v1/auth/register",
        "Period": "1h",
        "Limit": 3
      },
      {
        "Endpoint": "POST:/api/v1/auth/forgot-password",
        "Period": "1h",
        "Limit": 3
      },
      {
        "Endpoint": "*",
        "Period": "1s",
        "Limit": 10
      }
    ]
  }
}
```

## 5. CORS Configuration

### 5.1 Production CORS Settings

**CORS Policy Configuration**:
```json
{
  "CORS": {
    "DefaultPolicy": {
      "AllowedOrigins": [
        "https://whoandwhat.com",
        "https://www.whoandwhat.com",
        "https://app.whoandwhat.com"
      ],
      "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "AllowedHeaders": ["*"],
      "AllowCredentials": true
    },
    "MobilePolicy": {
      "AllowedOrigins": ["capacitor://localhost", "ionic://localhost"],
      "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "AllowedHeaders": ["*"],
      "AllowCredentials": true
    }
  }
}
```

## 6. Monitoring and Logging

### 6.1 Application Insights Configuration

**Application Insights Setup**:
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "[from-azure-application-insights]",
    "EnableAdaptiveSampling": true,
    "EnableQuickPulseMetricStream": true,
    "EnableDeveloperMode": false
  }
}
```

### 6.2 Serilog Configuration

**Production Logging Configuration**:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "ApplicationInsights",
        "Args": {
          "restrictedToMinimumLevel": "Warning"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName"]
  }
}
```

## 7. Container Deployment

### 7.1 Docker Configuration

**Production Dockerfile**:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY published/ .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WhoAndWhat.API.dll"]
```

**Environment Variables**:
```bash
# Required environment variables
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
```

### 7.2 Azure Container Apps Configuration

**Container Apps Configuration**:
```yaml
properties:
  configuration:
    secrets:
      - name: keyvault-endpoint
        value: "https://whoandwhat-prod-kv.vault.azure.net/"
    registries:
      - server: whoandwhatacr.azurecr.io
        username: whoandwhatacr
        passwordSecretRef: registry-password
  template:
    containers:
      - name: whoandwhat-api
        image: whoandwhatacr.azurecr.io/whoandwhat-api:latest
        resources:
          cpu: 1.0
          memory: 2Gi
        env:
          - name: KeyVault__Endpoint
            secretRef: keyvault-endpoint
    scale:
      minReplicas: 2
      maxReplicas: 10
```

## 8. Deployment Validation

### 8.1 Health Check Endpoints

**Health Check URLs**:
```
GET https://api.whoandwhat.com/health
GET https://api.whoandwhat.com/health/live
GET https://api.whoandwhat.com/health/ready
```

### 8.2 Authentication Flow Testing

**Test Commands**:
```bash
# Test user registration
curl -X POST https://api.whoandwhat.com/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","username":"testuser","password":"SecurePass123!","acceptTerms":true}'

# Test user login
curl -X POST https://api.whoandwhat.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"SecurePass123!"}'

# Test OAuth providers
curl -X GET https://api.whoandwhat.com/api/v1/oauth/google
curl -X GET https://api.whoandwhat.com/api/v1/oauth/facebook
curl -X GET https://api.whoandwhat.com/api/v1/oauth/apple
```

### 8.3 Security Validation

**Security Headers Validation**:
```bash
# Test security headers
curl -I https://api.whoandwhat.com/api/v1/auth/login

# Expected headers:
# X-Content-Type-Options: nosniff
# X-Frame-Options: DENY
# Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
# Content-Security-Policy: [policy]
# Referrer-Policy: strict-origin-when-cross-origin
```

## 9. Monitoring and Alerting

### 9.1 Key Performance Indicators

**Monitoring Metrics**:
- **Authentication Success Rate**: > 99%
- **API Response Time**: < 500ms (95th percentile)
- **Database Connection Pool**: < 80% utilization
- **Memory Usage**: < 1.5GB per container
- **CPU Usage**: < 70% average

### 9.2 Alert Configuration

**Critical Alerts**:
1. **Authentication Failure Spike**: > 10% failure rate in 5 minutes
2. **High Response Time**: > 2 seconds average for 3 minutes
3. **Database Connection Failures**: > 5 failures in 1 minute
4. **Security Incidents**: Rate limiting triggered, DDoS detected
5. **Application Errors**: > 10 errors in 5 minutes

## 10. Security Hardening

### 10.1 Network Security

**Recommended Setup**:
- **Web Application Firewall (WAF)**: Azure Front Door or Cloudflare
- **DDoS Protection**: Azure DDoS Protection Standard
- **SSL/TLS**: TLS 1.3 with perfect forward secrecy
- **Certificate Management**: Automated certificate renewal

### 10.2 Data Protection

**Encryption Requirements**:
- **Data at Rest**: Azure Database encryption enabled
- **Data in Transit**: TLS 1.3 for all communications
- **Key Management**: Azure Key Vault with HSM backing
- **Backup Encryption**: Encrypted database backups

## 11. Backup and Disaster Recovery

### 11.1 Database Backup Strategy

**Backup Configuration**:
- **Daily Full Backups**: Automated with 30-day retention
- **Transaction Log Backups**: Every 15 minutes
- **Point-in-Time Recovery**: 7-day window
- **Cross-Region Backup**: Secondary region for disaster recovery

### 11.2 Application Recovery

**Recovery Strategy**:
- **Multi-Region Deployment**: Primary and secondary regions
- **Automated Failover**: Health check-based failover
- **Data Synchronization**: Real-time database replication
- **Recovery Time Objective (RTO)**: < 15 minutes
- **Recovery Point Objective (RPO)**: < 5 minutes

## 12. Compliance and Auditing

### 12.1 Data Privacy Compliance

**GDPR/CCPA Requirements**:
- **Data Encryption**: All PII encrypted at rest and in transit
- **Data Retention**: Configurable user data retention policies
- **Right to Deletion**: User account and data deletion capabilities
- **Data Export**: User data export functionality
- **Audit Logging**: Comprehensive audit trail for data access

### 12.2 Security Auditing

**Audit Requirements**:
- **Authentication Events**: All login attempts logged
- **Authorization Events**: Permission changes tracked
- **Data Access**: PII access logging
- **Configuration Changes**: Security configuration changes audited
- **Incident Response**: Security incident documentation

## Conclusion

This production deployment guide provides comprehensive configuration requirements for deploying the WhoAndWhat API authentication system. The system is designed with enterprise-grade security, performance, and reliability in mind.

**Pre-Deployment Checklist**:
- ✅ Azure Key Vault configured with all required secrets
- ✅ OAuth providers configured with production credentials
- ✅ SMTP service configured for email delivery
- ✅ Database migrations applied to production database
- ✅ Security headers and policies configured
- ✅ Rate limiting and DDoS protection enabled
- ✅ Monitoring and alerting configured
- ✅ Backup and disaster recovery procedures in place

**Post-Deployment Validation**:
- ✅ Health checks responding successfully
- ✅ Authentication flows working correctly
- ✅ OAuth providers functioning properly
- ✅ Email delivery working
- ✅ Security headers present and correct
- ✅ Monitoring and alerting active

The authentication system is production-ready and provides a secure foundation for the WhoAndWhat application.