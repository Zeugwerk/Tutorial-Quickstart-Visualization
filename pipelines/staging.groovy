#!/usr/bin/env groovy

pipeline {
  agent { label 'GODOT' }
  options {
    disableConcurrentBuilds()
    skipDefaultCheckout(true)
    timeout(time: 15, unit: 'MINUTES')
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
              copyArtifacts filter: 'bindings/Zeugwerk Quickstart/Classes.cs,bindings/Zeugwerk Quickstart/Enums.cs,bindings/Zeugwerk Quickstart/Structs.cs', projectName: b.getFullProjectName() as String, selector: upstream(), target: '.', flatten: true, optional: false 
            }
          }
          
          // write build information
          def references = [:]
          references.revision = git.GIT_COMMIT
          writeJSON file: 'references.json', json: references, pretty: 2
          archiveArtifacts artifacts: "references.json"          
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
          archiveArtifacts artifacts: 'Tutorial_Quickstart_Visualization.exe,Tutorial_Quickstart_Visualization.pck,data_Visualization/**/*'
        }
      }
    }   
  }
}
