# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2020-05-26
### Added
- MaintenanceController
    - Flood endpoint to send pre-existing matches for re-analysis (Testing)

## [1.0.0] - 2020-04-08
### Added
- Code for MatchesLooker, which is deactivated but already merged to avoid future conflicts
- MatchesLooker env vars: MATCHES_LOOKER_MAX_USERS, MATCHES_LOOKER_PERIOD_DAYS, MATCHES_LOOKER_ACTIVITY_TIMESPAN
- Env var BASE_HTTP_MENTORINTERFACE

###Changed
- WorkUser now calls MentorInterface for quality instead of getting it as a parameter

## [0.2.0] - 2020-03-19
### Changed
- FACEIT_OAUTH_TOKEN_ENDPOINT is now optional, with a hardcoded default value.

## [0.2.0] - 2020-03-10
### Added
- CI
- Automatically run migrations on Startup.

### Changed
- Logging with correct timestamp
- Dockerfile
- Startup throws exceptions on missing env vars
