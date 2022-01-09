#!/usr/bin/env groovy

pipeline {
  agent { label 'GODOT' }
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
        bat "cmdkey /generic:TERMSRV/werkstatt2 /user:zeugwerker /pass:1"
        bat "mstsc /v:werkstatt2 /w:640 /h:480"         
        bat "nuget.exe restore"
        dir('bin') { deleteDir() }
        bat "mkdir bin"
        bat "Godot.exe --no-window --export \"Windows Desktop\" \"${WORKSPACE}\\bin\\Tutorial_Quickstart_Visualization.exe\""
        bat "cmdkey /delete:TERMSRV/werkstatt2"
        dir('bin') {
          archiveArtifacts artifacts: '**'
        }
      }
    }   
  }
}
