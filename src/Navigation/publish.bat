msbuild /t:restore,build,pack /nowarn:MSB4011,VSX1000 /p:Configuration=Release /p:NoPackageAnalysis=true /p:ContinuousIntegrationBuild=true /verbosity:minimal