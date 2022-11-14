#!/usr/bin/env groovy

pipeline {
  agent { label 'GODOT' }
  options {
    disableConcurrentBuilds()
    skipDefaultCheckout(true)
    timeout(time: 15, unit: 'MINUTES')
  }
  
  triggers {
    pollSCM '@monthly'
  }
  
  stages {
    stage('Configure') {
      steps {
        script {
          def git = checkout scm
          
          // increment version
          env.version = increment_version fallback: '0.0.0.0'
          currentBuild.displayName = env.version
          
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
        script {
          bat "nuget.exe restore"
          dir('bin') { deleteDir() }
          bat "mkdir bin"
          
          ret = bat "Godot.exe --no-window --export \"Windows Desktop\" \"${WORKSPACE}\\bin\\Tutorial_Quickstart_Visualization.exe\"", returnStdout: true
          println ret
          if(ret.contains("System.Exception: Failed to build project")) {
            error "GODOT project had build errors!" 
          }
          
          dir('bin') {
            archiveArtifacts artifacts: 'Tutorial_Quickstart_Visualization.exe,Tutorial_Quickstart_Visualization.pck,data_Visualization/**/*'
          }
        }
      }
    }   
  }
  
  post {
    success {
      script {
        tag_version()
      }
    }            
  }      
}
