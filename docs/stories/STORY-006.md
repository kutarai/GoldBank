# STORY-006: CI/CD Pipeline Setup

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want an automated CI/CD pipeline,
So that code is built, tested, and deployed automatically.

---

## Description

### Background
Automated CI/CD is essential for maintaining code quality and enabling rapid, reliable deployments. With 11 projects in the GoldBank solution and multiple developers working in parallel on Sprint 1 stories, an automated pipeline ensures that every push to the repository is built, tested, and validated before merging.

The pipeline covers the full lifecycle from code commit to staging deployment. It builds all projects, runs unit and integration tests with coverage reporting, builds Docker images for each service, pushes them to a container registry, and deploys to the staging environment. The pipeline should enforce quality gates: builds must succeed, tests must pass, and code coverage must meet the minimum threshold.

### Scope

**In scope:**
- CI pipeline configuration file (GitLab CI `.gitlab-ci.yml` or Jenkins `Jenkinsfile`)
- Build stage: `dotnet restore` and `dotnet build` for the full solution
- Test stage: `dotnet test` with coverage collection and reporting
- Docker stage: Build Docker images for all services using multi-stage Dockerfiles
- Push stage: Tag and push images to container registry
- Deploy stage: Deploy to staging environment via Docker Compose
- Pipeline triggers: push to `main`, push to feature branches, merge requests
- Coverage threshold enforcement (>=80%)
- Build matrix approach for parallel project builds
- Pipeline status badges for repository README

**Out of scope:**
- Production deployment pipeline (separate story with approval gates)
- Blue/green or canary deployment strategies
- Infrastructure provisioning (Terraform, Ansible)
- Security scanning (SAST, DAST) -- separate security story
- Performance testing in pipeline
- Multi-environment promotion (dev -> staging -> prod)

### User Flow
1. Developer pushes code to a feature branch
2. CI pipeline triggers automatically
3. Pipeline runs stages sequentially: restore -> build -> test -> docker-build
4. Developer views pipeline status in GitLab/Jenkins dashboard
5. If any stage fails, developer receives notification and reviews logs
6. On merge to `main`, full pipeline runs including docker-push and deploy-staging
7. Staging environment is updated with latest images
8. Team validates changes on staging before production release

---

## Acceptance Criteria

- [ ] CI pipeline configuration file exists in the repository root
- [ ] Pipeline triggers on: push to `main`, push to feature branches, merge requests
- [ ] **Restore stage**: `dotnet restore` completes for the full solution
- [ ] **Build stage**: `dotnet build` completes without errors for all 11 projects
- [ ] **Test stage**: `dotnet test` runs all test projects with coverage collection
- [ ] **Test stage**: Coverage report is generated in Cobertura XML format
- [ ] **Test stage**: Pipeline fails if code coverage drops below 80%
- [ ] **Docker stage**: Docker images are built for all 8 deployable services
- [ ] **Docker stage**: Images are tagged with commit SHA and branch name
- [ ] **Push stage**: Images are pushed to container registry (on `main` branch only)
- [ ] **Deploy stage**: Staging environment is updated with new images (on `main` branch only)
- [ ] Pipeline completes in under 15 minutes for a full run
- [ ] Failed pipelines send notification to development team
- [ ] Pipeline status badge is available for repository README

---

## Technical Notes

### Components

**Pipeline Configuration Files:**
```
repository-root/
  .gitlab-ci.yml          # GitLab CI configuration (primary)
  Jenkinsfile             # Jenkins alternative (if GitLab not available)
  docker/
    build/
      Dockerfile.gateway
      Dockerfile.core
      Dockerfile.switching
      Dockerfile.terminal-manager
      Dockerfile.hsm
      Dockerfile.admin
      Dockerfile.reporting
      Dockerfile.notifications
  scripts/
    ci/
      run-tests.sh
      build-images.sh
      deploy-staging.sh
```

### GitLab CI Configuration

**.gitlab-ci.yml:**
```yaml
stages:
  - restore
  - build
  - test
  - docker-build
  - docker-push
  - deploy-staging

variables:
  DOTNET_VERSION: "10.0"
  DOCKER_REGISTRY: "registry.example.com/goldbank"
  SOLUTION_FILE: "GoldBank.sln"
  COVERAGE_THRESHOLD: "80"

# Shared configuration
.dotnet-job:
  image: mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}
  cache:
    key: dotnet-packages-${CI_COMMIT_REF_SLUG}
    paths:
      - .nuget/

# ============================================================
# Stage: Restore
# ============================================================
restore:
  extends: .dotnet-job
  stage: restore
  script:
    - dotnet restore ${SOLUTION_FILE} --packages .nuget/
  artifacts:
    paths:
      - .nuget/
    expire_in: 1 hour

# ============================================================
# Stage: Build
# ============================================================
build:
  extends: .dotnet-job
  stage: build
  needs: [restore]
  script:
    - dotnet build ${SOLUTION_FILE} --no-restore -c Release
      -p:TreatWarningsAsErrors=true
  artifacts:
    paths:
      - src/*/bin/
      - src/*/obj/
      - tests/*/bin/
      - tests/*/obj/
    expire_in: 1 hour

# ============================================================
# Stage: Test
# ============================================================
test-unit:
  extends: .dotnet-job
  stage: test
  needs: [build]
  script:
    - dotnet test tests/GoldBank.Tests/GoldBank.Tests.csproj
      --no-build -c Release
      --collect:"XPlat Code Coverage"
      --results-directory ./coverage
      --logger "junit;LogFilePath=./test-results/results.xml"
    - |
      # Check coverage threshold
      COVERAGE=$(grep -oP 'line-rate="\K[0-9.]+' coverage/*/coverage.cobertura.xml | head -1)
      COVERAGE_PCT=$(echo "$COVERAGE * 100" | bc)
      echo "Code coverage: ${COVERAGE_PCT}%"
      if (( $(echo "$COVERAGE_PCT < ${COVERAGE_THRESHOLD}" | bc -l) )); then
        echo "Coverage ${COVERAGE_PCT}% is below threshold ${COVERAGE_THRESHOLD}%"
        exit 1
      fi
  artifacts:
    when: always
    paths:
      - coverage/
      - test-results/
    reports:
      junit: test-results/results.xml
      coverage_report:
        coverage_format: cobertura
        path: coverage/*/coverage.cobertura.xml
    expire_in: 7 days
  coverage: '/Code coverage: (\d+\.?\d*)%/'

test-integration:
  extends: .dotnet-job
  stage: test
  needs: [build]
  services:
    - name: postgres:18
      alias: postgres
      variables:
        POSTGRES_USER: goldbank_test
        POSTGRES_PASSWORD: test_password
        POSTGRES_DB: goldbank_test
    - name: redis:7-alpine
      alias: redis
  variables:
    ConnectionStrings__PostgreSQL: "Host=postgres;Port=5432;Database=goldbank_test;Username=goldbank_test;Password=test_password"
    ConnectionStrings__Redis: "redis:6379"
  script:
    - dotnet test tests/GoldBank.IntegrationTests/GoldBank.IntegrationTests.csproj
      --no-build -c Release
      --logger "junit;LogFilePath=./test-results/integration-results.xml"
  artifacts:
    when: always
    reports:
      junit: test-results/integration-results.xml
    expire_in: 7 days
  allow_failure: false

# ============================================================
# Stage: Docker Build
# ============================================================
.docker-build-job:
  stage: docker-build
  image: docker:24
  services:
    - docker:24-dind
  needs: [test-unit, test-integration]
  before_script:
    - docker login -u ${CI_REGISTRY_USER} -p ${CI_REGISTRY_PASSWORD} ${CI_REGISTRY}

docker-build-gateway:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.gateway
      -t ${DOCKER_REGISTRY}/gateway:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/gateway:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/gateway:${CI_COMMIT_SHA} > gateway.tar
  artifacts:
    paths:
      - gateway.tar
    expire_in: 1 hour

docker-build-core:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.core
      -t ${DOCKER_REGISTRY}/core:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/core:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/core:${CI_COMMIT_SHA} > core.tar
  artifacts:
    paths:
      - core.tar
    expire_in: 1 hour

docker-build-switching:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.switching
      -t ${DOCKER_REGISTRY}/switching:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/switching:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/switching:${CI_COMMIT_SHA} > switching.tar
  artifacts:
    paths:
      - switching.tar
    expire_in: 1 hour

docker-build-terminal-manager:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.terminal-manager
      -t ${DOCKER_REGISTRY}/terminal-manager:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/terminal-manager:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/terminal-manager:${CI_COMMIT_SHA} > terminal-manager.tar
  artifacts:
    paths:
      - terminal-manager.tar
    expire_in: 1 hour

docker-build-hsm:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.hsm
      -t ${DOCKER_REGISTRY}/hsm:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/hsm:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/hsm:${CI_COMMIT_SHA} > hsm.tar
  artifacts:
    paths:
      - hsm.tar
    expire_in: 1 hour

docker-build-admin:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.admin
      -t ${DOCKER_REGISTRY}/admin:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/admin:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/admin:${CI_COMMIT_SHA} > admin.tar
  artifacts:
    paths:
      - admin.tar
    expire_in: 1 hour

docker-build-reporting:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.reporting
      -t ${DOCKER_REGISTRY}/reporting:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/reporting:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/reporting:${CI_COMMIT_SHA} > reporting.tar
  artifacts:
    paths:
      - reporting.tar
    expire_in: 1 hour

docker-build-notifications:
  extends: .docker-build-job
  script:
    - docker build -f docker/build/Dockerfile.notifications
      -t ${DOCKER_REGISTRY}/notifications:${CI_COMMIT_SHA}
      -t ${DOCKER_REGISTRY}/notifications:${CI_COMMIT_REF_SLUG}
      .
    - docker save ${DOCKER_REGISTRY}/notifications:${CI_COMMIT_SHA} > notifications.tar
  artifacts:
    paths:
      - notifications.tar
    expire_in: 1 hour

# ============================================================
# Stage: Docker Push (main branch only)
# ============================================================
docker-push:
  stage: docker-push
  image: docker:24
  services:
    - docker:24-dind
  needs:
    - docker-build-gateway
    - docker-build-core
    - docker-build-switching
    - docker-build-terminal-manager
    - docker-build-hsm
    - docker-build-admin
    - docker-build-reporting
    - docker-build-notifications
  rules:
    - if: '$CI_COMMIT_BRANCH == "main"'
  before_script:
    - docker login -u ${CI_REGISTRY_USER} -p ${CI_REGISTRY_PASSWORD} ${CI_REGISTRY}
  script:
    - |
      for service in gateway core switching terminal-manager hsm admin reporting notifications; do
        docker load < ${service}.tar
        docker push ${DOCKER_REGISTRY}/${service}:${CI_COMMIT_SHA}
        docker push ${DOCKER_REGISTRY}/${service}:${CI_COMMIT_REF_SLUG}
        docker tag ${DOCKER_REGISTRY}/${service}:${CI_COMMIT_SHA} ${DOCKER_REGISTRY}/${service}:latest
        docker push ${DOCKER_REGISTRY}/${service}:latest
      done

# ============================================================
# Stage: Deploy to Staging (main branch only)
# ============================================================
deploy-staging:
  stage: deploy-staging
  image: alpine:latest
  needs: [docker-push]
  rules:
    - if: '$CI_COMMIT_BRANCH == "main"'
  before_script:
    - apk add --no-cache openssh-client docker-compose
    - eval $(ssh-agent -s)
    - echo "$STAGING_SSH_KEY" | ssh-add -
  script:
    - ssh ${STAGING_USER}@${STAGING_HOST} "
        cd /opt/goldbank &&
        export IMAGE_TAG=${CI_COMMIT_SHA} &&
        docker compose pull &&
        docker compose up -d --remove-orphans &&
        docker compose ps
      "
  environment:
    name: staging
    url: https://staging.goldbank.example.com
```

### Jenkins Alternative

**Jenkinsfile:**
```groovy
pipeline {
    agent any

    environment {
        DOTNET_VERSION = '10.0'
        DOCKER_REGISTRY = 'registry.example.com/goldbank'
        SOLUTION_FILE = 'GoldBank.sln'
    }

    stages {
        stage('Restore') {
            steps {
                sh 'dotnet restore ${SOLUTION_FILE}'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build ${SOLUTION_FILE} --no-restore -c Release'
            }
        }

        stage('Test') {
            parallel {
                stage('Unit Tests') {
                    steps {
                        sh '''
                            dotnet test tests/GoldBank.Tests/GoldBank.Tests.csproj \
                                --no-build -c Release \
                                --collect:"XPlat Code Coverage" \
                                --results-directory ./coverage
                        '''
                    }
                    post {
                        always {
                            publishCoverage adapters: [coberturaAdapter('coverage/*/coverage.cobertura.xml')]
                        }
                    }
                }
                stage('Integration Tests') {
                    steps {
                        sh '''
                            docker compose -f docker-compose.test.yml up -d postgres redis
                            dotnet test tests/GoldBank.IntegrationTests/GoldBank.IntegrationTests.csproj \
                                --no-build -c Release
                        '''
                    }
                    post {
                        always {
                            sh 'docker compose -f docker-compose.test.yml down'
                        }
                    }
                }
            }
        }

        stage('Docker Build') {
            when { branch 'main' }
            steps {
                script {
                    def services = ['gateway', 'core', 'switching', 'terminal-manager',
                                   'hsm', 'admin', 'reporting', 'notifications']
                    def builds = [:]
                    services.each { svc ->
                        builds[svc] = {
                            sh """
                                docker build -f docker/build/Dockerfile.${svc} \
                                    -t ${DOCKER_REGISTRY}/${svc}:${env.GIT_COMMIT} \
                                    -t ${DOCKER_REGISTRY}/${svc}:latest .
                            """
                        }
                    }
                    parallel builds
                }
            }
        }

        stage('Docker Push') {
            when { branch 'main' }
            steps {
                withCredentials([usernamePassword(
                    credentialsId: 'docker-registry',
                    usernameVariable: 'DOCKER_USER',
                    passwordVariable: 'DOCKER_PASS')]) {
                    sh 'echo $DOCKER_PASS | docker login -u $DOCKER_USER --password-stdin ${DOCKER_REGISTRY}'
                    sh '''
                        for svc in gateway core switching terminal-manager hsm admin reporting notifications; do
                            docker push ${DOCKER_REGISTRY}/${svc}:${GIT_COMMIT}
                            docker push ${DOCKER_REGISTRY}/${svc}:latest
                        done
                    '''
                }
            }
        }

        stage('Deploy Staging') {
            when { branch 'main' }
            steps {
                sshagent(['staging-ssh-key']) {
                    sh """
                        ssh ${STAGING_USER}@${STAGING_HOST} '
                            cd /opt/goldbank &&
                            export IMAGE_TAG=${GIT_COMMIT} &&
                            docker compose pull &&
                            docker compose up -d --remove-orphans
                        '
                    """
                }
            }
        }
    }

    post {
        failure {
            emailext subject: "Pipeline Failed: \${env.JOB_NAME} [\${env.BUILD_NUMBER}]",
                     body: "Check: \${env.BUILD_URL}",
                     recipientProviders: [developers()]
        }
        always {
            cleanWs()
        }
    }
}
```

### Docker Image Tagging Strategy

| Tag | When | Purpose |
|-----|------|---------|
| `{commit-sha}` | Every build | Immutable reference to exact code version |
| `{branch-name}` | Every build | Latest build for branch |
| `latest` | Main branch only | Latest stable build |
| `v{version}` | Tagged releases | Release versions |

### API / gRPC Endpoints
Not applicable for this story.

### Database Changes
Not applicable for this story. Integration tests use a temporary test database.

### Security Considerations
- Store Docker registry credentials as CI/CD secrets (never in pipeline file)
- Store SSH keys for staging deployment as CI/CD secrets
- Never log secrets or credentials in pipeline output
- Use specific image tags (not `latest`) for CI runner images
- Scan Docker images for vulnerabilities (future enhancement)
- Pipeline should not have access to production credentials
- `.gitlab-ci.yml` changes should require review (protected file)

### Edge Cases
- Pipeline timeout: Set reasonable timeout (15 minutes total) to prevent hanging pipelines
- Flaky tests: Integration tests may occasionally fail due to timing; add retry mechanism (max 2 retries)
- Docker build cache: Use BuildKit with cache mounts to speed up builds
- Parallel job resource limits: Ensure CI runners have enough resources for parallel Docker builds
- Branch name sanitization: Some branch names may contain characters invalid for Docker tags; sanitize
- Large test output: Test artifacts should have retention policies to avoid storage bloat
- Rollback mechanism: If staging deployment fails, pipeline should indicate failure clearly

---

## Dependencies

**Prerequisite Stories:**
- STORY-001: Solution Scaffolding & Project Structure (must have solution to build)
- STORY-002: Docker Compose Development Environment (must have Dockerfiles to build images)

**Blocked Stories:**
- No stories are directly blocked by CI/CD, but all subsequent development benefits from it

**External Dependencies:**
- GitLab CI runner or Jenkins server
- Docker registry (GitLab Container Registry, Docker Hub, or private registry)
- Staging server with Docker and Docker Compose installed
- SSH access to staging server
- CI/CD secrets configured: `CI_REGISTRY_USER`, `CI_REGISTRY_PASSWORD`, `STAGING_SSH_KEY`, `STAGING_HOST`, `STAGING_USER`

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) -- pipeline itself is tested by running it
- [ ] Integration tests passing -- pipeline runs end-to-end successfully
- [ ] Code reviewed and approved
- [ ] Documentation updated (pipeline stages documented, README badge added)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging (this is verified by the pipeline itself)

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
