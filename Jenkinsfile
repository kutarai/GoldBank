// =============================================================================
// GoldBank CI/CD Pipeline - Jenkins
// .NET 10 Preview | Docker Compose v2 | Multi-service Build
// =============================================================================

pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        DOTNET_NOLOGO              = 'true'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        SOLUTION_FILE              = 'GoldBank.slnx'
        DOCKER_REGISTRY            = credentials('docker-registry-url')
        DOCKER_TAG                 = "${env.GIT_COMMIT?.take(8) ?: 'latest'}"
        COVERAGE_THRESHOLD         = '80'
    }

    options {
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    stages {
        // =====================================================================
        // Stage: Restore
        // =====================================================================
        stage('Restore') {
            agent {
                docker {
                    image 'mcr.microsoft.com/dotnet/sdk:10.0-preview'
                    reuseNode true
                }
            }
            steps {
                sh 'dotnet restore ${SOLUTION_FILE}'
            }
        }

        // =====================================================================
        // Stage: Build
        // =====================================================================
        stage('Build') {
            agent {
                docker {
                    image 'mcr.microsoft.com/dotnet/sdk:10.0-preview'
                    reuseNode true
                }
            }
            steps {
                sh 'dotnet build ${SOLUTION_FILE} --configuration Release --no-restore'
            }
        }

        // =====================================================================
        // Stage: Test (parallel unit + integration)
        // =====================================================================
        stage('Test') {
            parallel {
                stage('Unit Tests') {
                    agent {
                        docker {
                            image 'mcr.microsoft.com/dotnet/sdk:10.0-preview'
                            reuseNode true
                        }
                    }
                    steps {
                        sh '''
                            dotnet test tests/GoldBank.Tests/GoldBank.Tests.csproj \
                                --configuration Release \
                                --no-build \
                                --logger "junit;LogFilePath=../../test-results/unit-tests.xml" \
                                --collect:"XPlat Code Coverage" \
                                -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
                        '''
                    }
                    post {
                        always {
                            junit testResults: 'test-results/unit-tests.xml', allowEmptyResults: true
                            publishCoverage adapters: [
                                coberturaAdapter(path: '**/TestResults/**/coverage.cobertura.xml')
                            ]
                        }
                    }
                }

                stage('Integration Tests') {
                    agent {
                        docker {
                            image 'mcr.microsoft.com/dotnet/sdk:10.0-preview'
                            reuseNode true
                        }
                    }
                    steps {
                        sh '''
                            dotnet test tests/GoldBank.IntegrationTests/GoldBank.IntegrationTests.csproj \
                                --configuration Release \
                                --no-build \
                                --logger "junit;LogFilePath=../../test-results/integration-tests.xml" \
                                --collect:"XPlat Code Coverage" \
                                -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
                        '''
                    }
                    post {
                        always {
                            junit testResults: 'test-results/integration-tests.xml', allowEmptyResults: true
                        }
                    }
                }
            }
        }

        // =====================================================================
        // Stage: Coverage Threshold Enforcement
        // =====================================================================
        stage('Coverage Gate') {
            agent {
                docker {
                    image 'mcr.microsoft.com/dotnet/sdk:10.0-preview'
                    reuseNode true
                }
            }
            steps {
                sh '''
                    COVERAGE_FILE=$(find . -name "coverage.cobertura.xml" -path "*/GoldBank.Tests/*" | head -1)
                    if [ -n "$COVERAGE_FILE" ]; then
                        LINE_RATE=$(grep -oP 'line-rate="\\K[^"]+' "$COVERAGE_FILE" | head -1)
                        COVERAGE_PCT=$(echo "$LINE_RATE * 100" | bc -l | xargs printf "%.2f")
                        echo "Unit test coverage: ${COVERAGE_PCT}%"
                        THRESHOLD_MET=$(echo "$COVERAGE_PCT >= ${COVERAGE_THRESHOLD}" | bc -l)
                        if [ "$THRESHOLD_MET" -eq 0 ]; then
                            echo "ERROR: Coverage ${COVERAGE_PCT}% is below threshold ${COVERAGE_THRESHOLD}%"
                            exit 1
                        fi
                        echo "Coverage threshold met: ${COVERAGE_PCT}% >= ${COVERAGE_THRESHOLD}%"
                    else
                        echo "WARNING: No coverage report found"
                    fi
                '''
            }
        }

        // =====================================================================
        // Stage: Docker Build (parallel for all services)
        // =====================================================================
        stage('Docker Build') {
            parallel {
                stage('Build Gateway') {
                    steps {
                        sh "docker build -t ${DOCKER_REGISTRY}/goldbank/gateway:${DOCKER_TAG} -f server/GoldBank.Gateway/Dockerfile ."
                    }
                }
                stage('Build Terminal Manager') {
                    steps {
                        sh "docker build -t ${DOCKER_REGISTRY}/goldbank/terminal-manager:${DOCKER_TAG} -f terminal/GoldBank.TerminalManager/Dockerfile ."
                    }
                }
                stage('Build HSM') {
                    steps {
                        sh "docker build -t ${DOCKER_REGISTRY}/goldbank/hsm:${DOCKER_TAG} -f hsm/GoldBank.HSM/Dockerfile ."
                    }
                }
                stage('Build Admin') {
                    steps {
                        sh "docker build -t ${DOCKER_REGISTRY}/goldbank/admin:${DOCKER_TAG} -f admin/GoldBank.Admin/Dockerfile ."
                    }
                }
                stage('Build Notifications') {
                    steps {
                        sh "docker build -t ${DOCKER_REGISTRY}/goldbank/notifications:${DOCKER_TAG} -f server/GoldBank.Notifications/Dockerfile ."
                    }
                }
            }
        }

        // =====================================================================
        // Stage: Docker Push (main branch only)
        // =====================================================================
        stage('Docker Push') {
            when {
                branch 'main'
            }
            steps {
                withCredentials([usernamePassword(
                    credentialsId: 'docker-registry-credentials',
                    usernameVariable: 'DOCKER_USER',
                    passwordVariable: 'DOCKER_PASS'
                )]) {
                    sh "echo ${DOCKER_PASS} | docker login ${DOCKER_REGISTRY} -u ${DOCKER_USER} --password-stdin"

                    sh """
                        # Tag and push all service images with commit SHA and latest
                        for SERVICE in gateway switching terminal-manager hsm admin notifications; do
                            docker tag ${DOCKER_REGISTRY}/goldbank/\${SERVICE}:${DOCKER_TAG} \
                                       ${DOCKER_REGISTRY}/goldbank/\${SERVICE}:latest
                            docker push ${DOCKER_REGISTRY}/goldbank/\${SERVICE}:${DOCKER_TAG}
                            docker push ${DOCKER_REGISTRY}/goldbank/\${SERVICE}:latest
                        done
                    """
                }
            }
        }

        // =====================================================================
        // Stage: Deploy Staging (main branch only)
        // =====================================================================
        stage('Deploy Staging') {
            when {
                branch 'main'
            }
            steps {
                withCredentials([usernamePassword(
                    credentialsId: 'docker-registry-credentials',
                    usernameVariable: 'DOCKER_USER',
                    passwordVariable: 'DOCKER_PASS'
                )]) {
                    sh "echo ${DOCKER_PASS} | docker login ${DOCKER_REGISTRY} -u ${DOCKER_USER} --password-stdin"

                    sh """
                        export IMAGE_TAG=${DOCKER_TAG}
                        docker compose -f docker-compose.yml --profile core pull
                        docker compose -f docker-compose.yml --profile infra --profile core up -d
                    """
                }
            }
        }
    }

    post {
        always {
            cleanWs()
        }
        success {
            echo 'GoldBank pipeline completed successfully.'
        }
        failure {
            echo 'GoldBank pipeline failed. Check the logs for details.'
        }
    }
}
