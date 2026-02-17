workspace "Happy Headlines" "C1 + C2 diagrams" {

  model {
    publisher = person "Publisher" "Drafts, reviews, and publishes articles."
    reader    = person "Reader" "Reads articles, comments, subscribes, shares."

    emailProvider = softwareSystem "Email Delivery Provider" "External system used to send newsletters." {
      tags "External"
    }

    hh = softwareSystem "Happy Headlines" "Positive news website and newsletter platform." {

      webapp = container "Webapp" "Internal editorial UI for publishers." "Web Application" {
        tags "Web"
      }

      website = container "Website" "Public website for readers." "Web Application" {
        tags "Web"
      }

      draftService = container "DraftService" "Stores and retrieves drafts." "Microservice (REST API)" {
        tags "Service"
      }

      publisherService = container "PublisherService" "Finalises publication of articles." "Microservice (REST API)" {
        tags "Service"
      }

      profanityService = container "ProfanityService" "Checks text against prohibited words." "Microservice (REST API)" {
        tags "Service"
      }

      commentService = container "CommentService" "Stores/reads comments; checks profanity." "Microservice (REST API)" {
        tags "Service"
      }

      articleService = container "ArticleService" "Serves articles; ingests new articles from the queue." "Microservice (REST API)" {
        tags "Service"
      }

      subscriberService = container "SubscriberService" "Handles newsletter subscriptions." "Microservice (REST API)" {
        tags "Service"
      }

      newsletterService = container "NewsletterService" "Builds and sends daily newsletters." "Microservice" {
        tags "Service"
      }

      articleQueue = container "ArticleQueue" "Approved articles are queued here." "Message Queue" {
        tags "Queue"
      }

      subscriberQueue = container "SubscriberQueue" "New subscriber events are queued here." "Message Queue" {
        tags "Queue"
      }

      draftDb = container "DraftDatabase" "Stores drafts." "Database" {
        tags "Database"
      }

      profanityDb = container "ProfanityDatabase" "Stores prohibited words." "Database" {
        tags "Database"
      }

      commentDb = container "CommentDatabase" "Stores comments." "Database" {
        tags "Database"
      }

      articleDb = container "ArticleDatabase" "Stores published articles." "Database" {
        tags "Database"
      }

      subscriberDb = container "SubscriberDatabase" "Stores subscriber information." "Database" {
        tags "Database"
      }
    }

    // People -> system (C1)
    publisher -> hh "Uses the platform" "Web Browser (HTTPS)"
    reader    -> hh "Uses the platform" "Web Browser (HTTPS)"
    hh -> emailProvider "Sends newsletters" "SMTP"

    // People -> UIs (C2)
    publisher -> webapp "Drafts/reviews/publishes" "HTTPS"
    reader -> website "Reads/comments/subscribes" "HTTPS"

    // Drafts
    webapp -> draftService "Save/load drafts" "HTTPS/REST"
    draftService -> draftDb "Read/write" "SQL"

    // Publishing
    webapp -> publisherService "Publish article" "HTTPS/REST"
    publisherService -> profanityService "Check profanity" "HTTPS/REST"
    profanityService -> profanityDb "Read/write" "SQL"
    publisherService -> articleQueue "Queue approved article" "Async message"

    // Articles
    website -> articleService "Fetch articles" "HTTPS/REST"
    articleService -> articleQueue "Consumes new articles" "Async message"
    articleService -> articleDb "Read/write" "SQL"

    // Comments
    website -> commentService "Post/read comments" "HTTPS/REST"
    commentService -> profanityService "Filter comments" "HTTPS/REST"
    commentService -> commentDb "Read/write" "SQL"

    // Subscribers + newsletter
    website -> subscriberService "Subscribe/unsubscribe" "HTTPS/REST"
    subscriberService -> subscriberDb "Read/write" "SQL"
    subscriberService -> subscriberQueue "Queue new subscriber event" "Async message"

    newsletterService -> articleService "Fetch recent articles" "HTTPS/REST"
    newsletterService -> subscriberService "Fetch active subscribers" "HTTPS/REST"
    newsletterService -> emailProvider "Send newsletter" "SMTP"
  }

  views {
    systemContext hh "C1" "System Context" {
      include publisher
      include reader
      include hh
      include emailProvider
      autolayout lr
    }

    container hh "C2" "Container view" {
      include *
      include publisher
      include reader
      include emailProvider
      autolayout lr
    }

    

    styles {
      element "Person" {
        shape Person
        background #0b5394
        color #ffffff
        stroke #073763
        strokeWidth 2
        fontSize 28
      }

      element "Software System" {
        shape RoundedBox
        background #1168bd
        color #ffffff
        stroke #0b4f8a
        strokeWidth 2
        fontSize 28
      }

      element "Web" {
        shape WebBrowser
        background #1c7ed6
        color #ffffff
        stroke #155a9c
        strokeWidth 2
      }

      element "Service" {
        shape Hexagon
        background #1f77b4
        color #ffffff
        stroke #155a9c
        strokeWidth 2
      }

      element "Database" {
        shape Cylinder
        background #b6d7a8
        color #000000
        stroke #6aa84f
        strokeWidth 2
      }

      element "Queue" {
        shape Pipe
        background #f9cb9c
        color #000000
        stroke #e69138
        strokeWidth 2
      }

      element "External" {
        background #8e7cc3
        color #ffffff
        stroke #674ea7
        strokeWidth 2
      }

      relationship "Relationship" {
        color #000000
        thickness 2
        fontSize 20
      }
    }
  }

  configuration {
    scope softwareSystem
  }
  
}
