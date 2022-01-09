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
          def git = checkout scm
          
          // get bindings from upstream if available
          dir("bindings/Tutorial_Quickstart") { 
            currentBuild.upstreamBuilds?.each { b -> 
              println "CopyArtifacts from ${b.getFullProjectName()}"
              copyArtifacts filter: 'bindings/Enums.cs,bindings/Structs.cs', projectName: b.getFullProjectName() as String, selector: upstream(), target: '.', flatten: true, optional: true 
            }
          }          
        }
      }
    }
    
    stage('Build') {
      steps {
        bat "nuget.exe restore"
        dir('bin') { deleteDir() }
        bat "mkdir bin"
        bat "Godot.exe --no-window --export \"Windows Desktop\" \"${WORKSPACE}\\bin\\Tutorial_Quickstart_Visualization.exe\""  
        dir('bin') {
          archiveArtifacts artifacts: '**'
        }
      }
    }   
  }
}
