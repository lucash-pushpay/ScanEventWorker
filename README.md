## Assumptions
1. The requirements list RunId as a required field, which is nested inside the User object. However, it doesn't specify whether the User object itself is required - so in this application, the User object is assumed to be required
2. The Scan Event API Detail specifies that EventId is a unique identifier for each event, but doesn't clarify whether it's an auto-incrementing field or another format. This application treats EventId as a UUID/GUID, as in real-world scenarios, to avoid potential collisions
3. This application assumes that CreatedDateTimeUtc is accurate and precise. With millisecond precision, timestamp collisions are rare. However, without sufficient precision, data inconsistency may occur when determining the latest event for a parcel
4. This application assumes that ParcelId is unique for each parcel, with only one record per parcel in the parcel table to maintain the latest parcel information
5. This application is designed to be fault-tolerant and resilient. Assuming the API may be unreliable and network issues may occur, it implements retry logic with exponential backoff, state persistence, and graceful error handling
6. This application assumes that CreatedDateTimeUtc might return timestamps that are not truly UTC. By using Instant types, it resolves timezone issues (e.g. if this worker server is hosted in a different timezone, it will still work correctly)
7. This application assumes that the Type and Status Code follows Amazon Style, and validation is implemented to ensure only known combinations are processed. Unknown types or status codes are logged as warnings and skipped
```
Order Received                          → STATUS: "ORDER_RECEIVED"
Preparing for Shipment                  → STATUS: "PREPARING"
Shipped                                 → PICKUP: "DISPATCHED"
In Transit                              → STATUS: "IN_TRANSIT"
Out for Delivery                        → STATUS: "OUT_FOR_DELIVERY"
Delivered                               → DELIVERY: "DELIVERED"
```

## Improvements
1. Add EventId validation at the API level or duplicate detection cache for recently processed events to prevent duplication
2. Implement a more sophisticated retry mechanism with jitter to prevent the thundering herd problem
3. Add localization support for different languages and regions
4. Implement a more advanced configuration management system, such as AWS App Config
5. Implement circuit breaker pattern for API calls
6. The application currently uses LiteDB, a file-based NoSQL database for local development. To improve scalability and performance, consider migrating to a more robust NoSQL database such as DynamoDB
7. Add more unit tests and integration tests to cover more scenarios and edge cases
8. Add more metrics and monitoring to track the performance and health of the worker service
9. Add a web dashboard to monitor the status of the worker service, view logs, and manage configurations

### To enable another worker app downsteram to perform actions against the same scan events processed
1. Add Kinesis to publish events after they are processed, so that other services can subscribe to these events and perform actions accordingly
2. Implement a schema registry to manage the event schema and versioning, so that downstream services can handle schema changes gracefully
3. Add a dead letter queue to handle failed events that cannot be processed, so that they can be reviewed and reprocessed later
4. Implement a more robust error handling and retry mechanism for downstream services, so that they can recover from transient errors and failures
5. Add a monitoring and alerting system to track the health and performance of the entire event processing pipeline

#### High level architecture diagram for multi-worker architecture:

```
[Scan Event C# Application]
    ↓ (publishes scan events)
[Amazon Kinesis Data Stream] 
    ↓ (real-time processing)
├── [Lambda Function/ C# Console App Worker] (event router)
│   ↓ (publishes to SNS)
│   [Amazon SNS Topic: "scan-events"]
│   ↓ (fan-out to multiple SQS queues)
│   ├── [SQS Queue: eg. "payment-processing"] 
│   │   └── [Payment Worker App] → [Payment DB] → [DLQ: payment-errors]
│   ├── [SQS Queue: eg. "fraud-detection"]
│   │   └── [Fraud Detection Worker] → [ML Service] → [DLQ: fraud-errors]
│   └── [SQS Queue: eg. "notification-service"]
│       └── [Notification Worker] → [Email/SMS] → [DLQ: notification-errors]
└── [Amazon Kinesis Data Firehose] 
    ↓ (batch analytics pipeline)
    [Amazon Redshift Cluster]
    ↓ (analytics queries)
    [Data Aggregation Service (DAS)]

[AWS Glue Schema Registry] ← (validates all events)
[CloudWatch] ← (monitoring & alerting)
[AWS X-Ray] ← (distributed tracing)
```                                                      


