#!/usr/bin/env groovy

pipeline {
  agent { label 'VS2019' }
  options {
    disableConcurrentBuilds()
    skipDefaultCheckout(true)
    timeout(time: 15, unit: 'MINUTES')
    office365ConnectorWebhooks([[notifyBackToNormal: true, notifyFailure: true, notifyRepeatedFailure: false ]])
  }

  triggers {
    pollSCM 'H 5 * * *'
  }

  stages {
    stage('Configure') {
      steps {
        script {
          cleanWs()
          def git = checkout scm
        }
      }
    }
			
    stage('Build') {
      steps {
        bat "nuget.exe restore"
	dir('bin') { deleteDir() }
	bar "mkdir bin"
        bat "Godot.exe --no-window --export "Windows Desktop" "${WORKSPACE}\bin\Tutorial_Quickstart_Visualization.exe"     
      }
    }   
  }
}
