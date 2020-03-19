# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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