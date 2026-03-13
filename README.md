# Happy Headlines

Happy Headlines is a global media technology company with a clear mission: to spread positivity by sharing uplifting news from around the world. Founded a decade ago by a group of visionary journalists and software engineers, the company has since grown into a digital powerhouse, serving millions of daily users across all continents.

# Phase 1 - Week 5

Do C4 diagram for a WebApp Called Happy Headlines

C1- context 
C2- container



# Phase 2 - Week 6

Implement the ArticleService and the ArticleDatabase. The ArticleService needs four endpoints for Create, Read, Update and Delete. It must be implemented as a REST-based API.

You have to perform a x-axis split on the ArticleService so that three instances are available with a load balancer to take care of which instance will service the incoming request.

Furthermore you have to perform a z-axis split on the ArticleDatabase such that each continent has their own database. Along with the seven continent database an eighth database will contain news articles that’s relevant across the entire world - a global database so-to-speak.



# Phase 3 - Week 8

This week you are expected to implement CommentService, CommentDatabase, ProfanityService and ProfanityDatabase. The CommentService and the ProfanityService must be properly fault isolated following relevant principles of swimlanes.

In order to work with this properly your CommentService and ProfanityService must communicate directly without any gateway or UI as middlelayer. The following illustration is from my notes on the HappyHeadlines project.

Furthermore you are expected to implement a circuit breaker pattern into the CommentService to take over if the ProfanityService is no longer available.

## Testing
Stop ProfanityService: docker stop profanityservice
Post a comment → should still save
Status should be PendingReview
Start it back: docker start profanityservice



# Phase 4 - Week 9

Implement centralized logging and monitoring infrastructure for all services. Update the C4 diagram (levels 1 and 2) to reflect monitoring needs.

Build the DraftService and DraftDatabase with logging and tracing in mind. All services must send structured logs to a central repository (ELK Stack) that is reusable across the entire architecture.

Implement automatic log cleanup to prevent unbounded disk usage. Logs older than 7 days are automatically deleted via a cron-based cleanup service.

## Implementation Details

**Stack:** Serilog → Logstash → Elasticsearch → Kibana

**Services:** ArticleService, CommentService, ProfanityService, DraftService

**Cleanup:** Automatic deletion of logs older than 7 days (runs daily at 02:00)

## Testing

View logs in Kibana: `http://localhost:5601`
- Query by service name: `service:"ArticleService"`
- View recent migrations: `message:"migrated successfully"`

Verify cleanup is running: `docker logs log-cleanup`