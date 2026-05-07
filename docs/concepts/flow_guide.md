# Intelligent Concept Auto-Linking System

This document outlines the state flow and logic for the automated concept discovery and linking system within SmallURL.

## 1. System State Diagram

```mermaid
stateDiagram-v2
    [*] --> CandidateDetection: Blog Content Received
    
    state CandidateDetection {
        [*] --> RegexMatching: Extract Capitalized Phrases
        RegexMatching --> BlacklistFilter: Remove "The", "This", etc.
        BlacklistFilter --> LengthCheck: Filter < 4 chars (unless Strict)
        LengthCheck --> [*]: Candidates Ready
    }
    
    CandidateDetection --> RegistryCheck: Query Local DB
    
    state RegistryCheck {
        [*] --> CacheFound: Term exists in Concepts table
        [*] --> CacheMissing: Term is new
        
        CacheFound --> Verified: Confidence >= 0.9
        CacheFound --> Pending: 0.5 <= Confidence < 0.9
        CacheFound --> Blacklisted: Confidence < 0.5
    }
    
    CacheMissing --> SearchDiscovery: Query Microsoft Learn API
    
    state SearchDiscovery {
        [*] --> SimilarityScoring: Calculate Levenshtein Distance
        SimilarityScoring --> VerificationLoop
        
        state VerificationLoop {
            HighConfidence: Confidence = 0.95 (Verified)
            LowConfidence: Confidence = 0.50 (Review Needed)
            NoMatch: Confidence = 0.00 (Reject)
        }
    }
    
    VerificationLoop --> RegistryUpdate: Save to DB
    RegistryUpdate --> Verified: If High Confidence
    RegistryUpdate --> Pending: If Low Confidence
    
    Verified --> InjectionEngine: Provide Link
    Pending --> [*]: Skip Injection (Wait for Manual Approval)
    Blacklisted --> [*]: Skip Injection
    
    state InjectionEngine {
        [*] --> FirstOccurrence: Find first match in text
        FirstOccurrence --> DensityCheck: Gap > 100 chars?
        DensityCheck --> HTMLInjection: Insert <a> tag
        HTMLInjection --> [*]
    }
    
    InjectionEngine --> [*]: Content Processed
```

## 2. Logic Details

| Logic Component | Implementation | Goal |
| :--- | :--- | :--- |
| **Strict Registry** | Hardcoded list of short terms (C#, .NET, AI). | Prevents common acronym collisions. |
| **Similarity Scoring** | Levenshtein Distance algorithm. | Ensures "Signal" doesn't link to "SignalR". |
| **Density Control** | 100-character gap budget. | Prevents "Link Soup" in dense paragraphs. |
| **Parallel Discovery** | `SemaphoreSlim(5)` + `Task.WhenAll`. | Zero-lag builds for new content. |
| **Human-in-the-Loop** | Manual API endpoints (`/approve`, `/reject`). | Reaches 97%+ perfection over time. |

## 3. Manual Control Endpoints

- **List All**: `GET /api/concepts`
- **Verify**: `POST /api/concepts/{id}/approve`
- **Blacklist**: `POST /api/concepts/{id}/reject`
- **Delete**: `DELETE /api/concepts/{id}`

> [!TIP]
> Use the `X-Api-Key` header for all management requests.
