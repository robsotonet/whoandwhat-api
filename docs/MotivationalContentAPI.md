# Motivational Content Management API

## Overview

The Motivational Content Management API provides a comprehensive system for delivering personalized, AI-driven motivational content to users based on their behavior, preferences, and engagement patterns. The system includes advanced features like A/B testing, content optimization, and intelligent targeting.

## Features

- 🎯 **Personalized Content Delivery** - AI-driven content selection based on user analytics
- 📊 **A/B Testing Framework** - Statistical analysis and optimization
- 🕒 **Smart Scheduling** - Time-zone aware delivery optimization
- 📈 **Engagement Analytics** - Comprehensive tracking and insights
- 🔄 **Auto-Optimization** - Machine learning-powered content improvement
- 🎨 **Multi-Channel Delivery** - Dashboard, SignalR, Email, Push notifications

## Base URL

```
https://api.whoandwhat.com/api/v1/motivational-content
```

## Authentication

All endpoints require JWT authentication. Include the JWT token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

---

## Core Endpoints

### 1. Get Personalized Content

Retrieves personalized motivational content for the authenticated user.

**Endpoint:** `GET /personalized`

**Query Parameters:**
- `trigger` (optional): Content trigger type (`TaskCompleted`, `LoginStreak`, `DailyGoal`, etc.)
- `context` (optional): JSON object with additional context information

**Request Example:**
```bash
curl -X GET "https://api.whoandwhat.com/api/v1/motivational-content/personalized?trigger=TaskCompleted" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json"
```

**Response Example:**
```json
{
  "success": true,
  "data": {
    "content": {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "title": "🎉 Task Completed!",
      "message": "Great job completing that task! You're building momentum one accomplishment at a time.",
      "contentType": "Achievement",
      "category": "Productivity",
      "imageUrl": "https://cdn.whoandwhat.com/images/achievement-badge.png",
      "actionText": "View Progress",
      "actionUrl": "/dashboard/analytics",
      "metadata": {
        "mood": "Celebratory",
        "difficulty": "Easy",
        "expectedEngagement": "High"
      }
    },
    "personalizationScore": 0.92,
    "reasonCode": "CompletionStreak_Beginner_HighPerformance",
    "abTestGroup": "variant_a",
    "deliveryId": "456e7890-e89b-12d3-a456-426614174001"
  }
}
```

### 2. Update User Preferences

Updates the user's content preferences and delivery settings.

**Endpoint:** `PUT /preferences`

**Request Body:**
```json
{
  "isContentEnabled": true,
  "preferredFrequency": "Moderate",
  "maxDailyContent": 5,
  "maxWeeklyContent": 25,
  "allowWeekends": true,
  "allowAfterHours": false,
  "timeZone": "America/New_York",
  "preferredContentTypes": ["Achievement", "Insight", "Encouragement"],
  "preferredCategories": ["Productivity", "Wellness"],
  "preferredChannels": ["Dashboard", "Push"],
  "preferredDeliveryTimes": [
    { "hour": 9, "minute": 0, "weight": 0.8 },
    { "hour": 14, "minute": 0, "weight": 0.6 },
    { "hour": 18, "minute": 0, "weight": 0.4 }
  ],
  "personalizationSettings": {
    "useProductivityData": true,
    "useEmotionalState": true,
    "adaptToFeedback": true,
    "learningRate": 0.1
  }
}
```

**Response Example:**
```json
{
  "success": true,
  "data": {
    "message": "Preferences updated successfully",
    "updatedAt": "2024-03-15T10:30:00Z",
    "effectiveFrom": "2024-03-15T10:30:00Z"
  }
}
```

### 3. Record Content Engagement

Records user engagement with delivered content for analytics and optimization.

**Endpoint:** `POST /engagement`

**Request Body:**
```json
{
  "deliveryId": "456e7890-e89b-12d3-a456-426614174001",
  "engagementType": "Click",
  "engagementMetadata": {
    "timeSpent": 15.5,
    "clickPosition": { "x": 245, "y": 180 },
    "deviceType": "Mobile",
    "userAgent": "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15"
  },
  "feedback": {
    "rating": 4,
    "sentiment": "Positive",
    "tags": ["Helpful", "Motivating"]
  }
}
```

**Response Example:**
```json
{
  "success": true,
  "data": {
    "engagementId": "789e0123-e89b-12d3-a456-426614174002",
    "recordedAt": "2024-03-15T10:32:00Z",
    "personalizationUpdate": {
      "scoreAdjustment": 0.05,
      "newPersonalizationScore": 0.97
    }
  }
}
```

### 4. Get Content Performance Metrics

Retrieves analytics and performance metrics for content delivery.

**Endpoint:** `GET /analytics/performance`

**Query Parameters:**
- `startDate` (optional): Start date for analysis (ISO 8601 format)
- `endDate` (optional): End date for analysis (ISO 8601 format)
- `contentId` (optional): Specific content ID to analyze
- `groupBy` (optional): Group metrics by (`day`, `week`, `month`, `contentType`, `category`)

**Request Example:**
```bash
curl -X GET "https://api.whoandwhat.com/api/v1/motivational-content/analytics/performance?startDate=2024-03-01T00:00:00Z&endDate=2024-03-15T23:59:59Z&groupBy=contentType" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Response Example:**
```json
{
  "success": true,
  "data": {
    "totalDeliveries": 1250,
    "totalEngagements": 486,
    "overallEngagementRate": 0.3888,
    "averageEngagementScore": 3.7,
    "averagePersonalizationScore": 0.82,
    "averageEngagementLatency": "00:00:03.250",
    "channelPerformance": {
      "Dashboard": 234,
      "Push": 189,
      "Email": 63
    },
    "engagementTypeBreakdown": {
      "View": 486,
      "Click": 298,
      "Share": 45,
      "Like": 156
    },
    "userSegmentPerformance": {
      "Beginner": 0.42,
      "Intermediate": 0.38,
      "Expert": 0.31
    },
    "trend": {
      "trendDirection": "Improving",
      "trendMagnitude": 0.08,
      "trendConfidence": "Medium"
    },
    "lastUpdated": "2024-03-15T10:35:00Z"
  }
}
```

---

## Admin Endpoints

### 1. Create Content (Admin Only)

Creates new motivational content with targeting and A/B testing configuration.

**Endpoint:** `POST /admin/content`

**Request Body:**
```json
{
  "title": "🚀 Productivity Master!",
  "message": "Incredible! You've completed multiple high-priority tasks. Your systematic approach to getting things done is truly impressive.",
  "contentType": "Achievement",
  "category": "Productivity",
  "targetConditions": {
    "experienceLevel": "Expert",
    "minCompletionRate": 0.8,
    "categories": ["Productivity", "Learning"],
    "minStreakDays": 7
  },
  "priority": 95,
  "imageUrl": "https://cdn.whoandwhat.com/images/productivity-master.png",
  "actionText": "View Analytics",
  "actionUrl": "/dashboard/analytics",
  "isABTestEnabled": true,
  "abTestConfiguration": {
    "groups": ["control", "variant_a", "variant_b"],
    "weights": [0.34, 0.33, 0.33],
    "variants": {
      "variant_a": { "title": "🎯 Focus Champion!" },
      "variant_b": { "title": "⚡ Efficiency Expert!" }
    },
    "minSampleSize": 100,
    "significanceLevel": 0.05
  },
  "schedulingRules": {
    "allowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    "preferredHours": [9, 14, 18],
    "cooldownMinutes": 240
  },
  "metadata": {
    "mood": "Celebratory",
    "difficulty": "Expert",
    "expectedEngagement": "High",
    "tags": ["Achievement", "Productivity", "Expert"]
  }
}
```

### 2. Get A/B Test Results (Admin Only)

Retrieves detailed A/B testing results with statistical analysis.

**Endpoint:** `GET /admin/ab-tests/{contentId}/results`

**Response Example:**
```json
{
  "success": true,
  "data": {
    "contentId": "123e4567-e89b-12d3-a456-426614174000",
    "testStatus": "Active",
    "testStartDate": "2024-03-01T00:00:00Z",
    "sampleSize": 450,
    "groupResults": {
      "control": {
        "deliveries": 150,
        "engagements": 52,
        "engagementRate": 0.347,
        "averageEngagementScore": 3.6,
        "confidenceInterval": { "lower": 0.298, "upper": 0.396 }
      },
      "variant_a": {
        "deliveries": 150,
        "engagements": 67,
        "engagementRate": 0.447,
        "averageEngagementScore": 4.1,
        "confidenceInterval": { "lower": 0.394, "upper": 0.500 }
      },
      "variant_b": {
        "deliveries": 150,
        "engagements": 58,
        "engagementRate": 0.387,
        "averageEngagementScore": 3.8,
        "confidenceInterval": { "lower": 0.335, "upper": 0.439 }
      }
    },
    "statisticalSignificance": 0.023,
    "recommendedAction": "Promote variant_a",
    "projectedImprovement": 0.100,
    "confidenceLevel": 0.95
  }
}
```

---

## Integration Examples

### React/TypeScript Client

```typescript
interface MotivationalContentClient {
  getPersonalizedContent(trigger?: string): Promise<PersonalizedContentResult>;
  updatePreferences(preferences: UserContentPreferences): Promise<void>;
  recordEngagement(engagement: ContentEngagement): Promise<void>;
}

class MotivationalContentService implements MotivationalContentClient {
  constructor(private apiClient: ApiClient) {}

  async getPersonalizedContent(trigger?: string): Promise<PersonalizedContentResult> {
    const response = await this.apiClient.get('/motivational-content/personalized', {
      params: { trigger }
    });
    return response.data;
  }

  async updatePreferences(preferences: UserContentPreferences): Promise<void> {
    await this.apiClient.put('/motivational-content/preferences', preferences);
  }

  async recordEngagement(engagement: ContentEngagement): Promise<void> {
    await this.apiClient.post('/motivational-content/engagement', engagement);
  }
}

// Usage example
const contentService = new MotivationalContentService(apiClient);

// Get personalized content after task completion
const content = await contentService.getPersonalizedContent('TaskCompleted');

// Record user engagement
await contentService.recordEngagement({
  deliveryId: content.deliveryId,
  engagementType: 'Click',
  engagementMetadata: {
    timeSpent: 12.5,
    deviceType: 'Desktop'
  }
});
```

### C# Client

```csharp
public interface IMotivationalContentClient
{
    Task<PersonalizedContentResult> GetPersonalizedContentAsync(string? trigger = null);
    Task UpdatePreferencesAsync(UserContentPreferences preferences);
    Task RecordEngagementAsync(ContentEngagement engagement);
}

public class MotivationalContentClient : IMotivationalContentClient
{
    private readonly HttpClient _httpClient;
    
    public MotivationalContentClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PersonalizedContentResult> GetPersonalizedContentAsync(string? trigger = null)
    {
        var url = "/api/v1/motivational-content/personalized";
        if (!string.IsNullOrEmpty(trigger))
        {
            url += $"?trigger={Uri.EscapeDataString(trigger)}";
        }
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PersonalizedContentResult>(json);
    }
}
```

### SignalR Real-time Integration

```typescript
// SignalR connection for real-time content delivery
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/dashboard')
  .build();

// Listen for real-time motivational content
connection.on('MotivationalContentDelivery', (content: PersonalizedContentResult) => {
  displayMotivationalContent(content);
  
  // Auto-record view engagement
  recordEngagement({
    deliveryId: content.deliveryId,
    engagementType: 'View',
    engagementMetadata: {
      deliveryChannel: 'SignalR',
      autoRecorded: true
    }
  });
});

await connection.start();
```

---

## Error Handling

### Standard Error Response Format

```json
{
  "success": false,
  "error": {
    "code": "INVALID_PREFERENCES",
    "message": "Invalid content preferences provided",
    "details": {
      "field": "preferredFrequency",
      "reason": "Must be one of: Low, Moderate, High, VeryHigh"
    },
    "timestamp": "2024-03-15T10:45:00Z",
    "traceId": "abc123-def456-ghi789"
  }
}
```

### Common Error Codes

- `UNAUTHORIZED` (401) - Invalid or missing authentication
- `FORBIDDEN` (403) - Insufficient permissions for admin endpoints  
- `INVALID_PREFERENCES` (400) - Invalid preference settings
- `CONTENT_NOT_FOUND` (404) - Requested content doesn't exist
- `ENGAGEMENT_FAILED` (400) - Invalid engagement data
- `RATE_LIMITED` (429) - Too many requests
- `INTERNAL_ERROR` (500) - Server error

---

## Rate Limits

- **User Endpoints**: 100 requests per minute per user
- **Admin Endpoints**: 1000 requests per minute per admin
- **Engagement Recording**: 500 requests per minute per user

Rate limit headers are included in responses:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 85
X-RateLimit-Reset: 1647341400
```

---

## Webhooks (Optional)

Configure webhooks to receive real-time notifications about content performance and user engagement.

### Webhook Events

- `content.delivered` - Content was delivered to a user
- `content.engaged` - User engaged with content
- `abtest.significant` - A/B test reached statistical significance
- `optimization.completed` - Content optimization cycle finished

### Webhook Payload Example

```json
{
  "event": "content.engaged",
  "timestamp": "2024-03-15T10:50:00Z",
  "data": {
    "userId": "user123",
    "contentId": "content456",
    "engagementType": "Click",
    "deliveryChannel": "Push",
    "personalizationScore": 0.89
  }
}
```

---

## Support

- **Documentation**: [https://docs.whoandwhat.com/motivational-content](https://docs.whoandwhat.com/motivational-content)
- **API Status**: [https://status.whoandwhat.com](https://status.whoandwhat.com)
- **Support**: [support@whoandwhat.com](mailto:support@whoandwhat.com)
- **GitHub Issues**: [https://github.com/whoandwhat/api/issues](https://github.com/whoandwhat/api/issues)