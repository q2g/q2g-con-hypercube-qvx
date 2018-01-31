# q2g-con-hypercube-qvx
[![Downloads](https://m.sense2go.net/downloads.svg?q2g-con-hypercube-qvx)](https://m.sense2go.net/extension-package)

Qlik Connector that allows to consume a Qlik HyperCube / Table as new datasource

## Intro

![teaser](https://github.com/q2g/q2g-con-hypercube-qvx/raw/master/docs/teaser.gif "Short teaser")

## Install

### binary

1. [Download the ZIP](https://m.sense2go.net/extension-package)
2. Copy it to:
C:\Program Files (x86)\Common Files\Qlik\Custom Data\
or
%appdata%\\..\Local\Programs\Common Files\Qlik\Custom Data\
3. unzip

### source

1. Clone the Github Repo
2. Open the .sln in Visual Studio
3. Compile

## Logging

The Qlik Connector using the NLog libary.
You can change the log settings in the "NLog.config".

### change the log path

The default path is linked to the directory "%appdata%\q2g\q2g-con-hypercube-qvx".
This you can change in the specified attribute "fileName" and "archiveFileName".

### change the log level

The log level is set to "warn" in the standard case.
You can set this via the attribute "minlevel".
