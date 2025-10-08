 
# 🛠️ .NET Pre-build tool for static resources

## Install

Install as a .NET tool:

``` cmd
dotnet tool install --global Dm.Web.BuildStatic
```

## Usage

CLI:
``` cmd
Dm.Web.BuildStatic [config.json]

# OR

Dm.Web.BuildStatic # Uses dm.web.build.static.json config by default
```

Pre-build step:
``` xml
<Target Name="BuildStatic" BeforeTargets="PreBuildEvent">
	<!-- Install tool before use: dotnet tool install --global Dm.Web.BuildStatic -->

	<!-- Uses dm.web.build.static.json from project root -->
	<Exec Command="Dm.Web.BuildStatic" />

	<!-- Alternative: Uses PreBuild/config.json from project root -->
	<Exec Command="Dm.Web.BuildStatic PreBuild/config.json" />
</Target>

```

## Config format

``` json
{
	"use": {
		"list":  "Dm.Web.BuildStatic.Services.Stages.FilesSource+Builder",
		"each":  "Dm.Web.BuildStatic.Services.Stages.ForEach+Builder",
		"copy":  "Dm.Web.BuildStatic.Services.Stages.FileCopy+Builder",
		"minjs": "Dm.Web.BuildStatic.Services.Stages.MinifyJS+Builder",
		"gzip":  "Dm.Web.BuildStatic.Services.Stages.GzipStream+Builder"
	},
	"pipelines": [
		{
			"list": "TestStatic/js/**/*.js",
			"each": "string",
			"copy": "{path}.gz",
			"minjs":  null,
			"gzip": "Optimal"
		},
		{
			"list": "TestStatic/css/*.css",
			"each": "string",
			"copy": "{path}.gz",
			"gzip": null
		},
		{
			"list": "TestStatic/move-to-js.txt",
			"each": "string",
			"copy": "TestStatic/js/moved.js.gz",
			"minjs":  null,
			"gzip": null
		}
	]
}
```

The configuration has two main sections:

1. `use`: Defines available steps in format: `"step_name": "BuilderClassName"`
1. `pipelines`: Defines pipelines and their steps in format: `"step_name": <config_data>|null`