 
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
		"gzip":  "Dm.Web.BuildStatic.Services.Stages.GzipStream+Builder",
		"sprite":"Dm.Web.BuildStatic.Services.Stages.SVGSprite+Builder"
	},
	"pipelines": [
		{
			"list":  "TestStatic/js/**/*.js",
			"each":  "string",
			"copy":  "{path}.gz",
			"minjs": null,
			"gzip":  "Optimal"
		},
		{
			"list": "TestStatic/css/*.css",
			"each": "string",
			"copy": "{path}.gz",
			"gzip": null
		},
		{
			"list":  "TestStatic/move-to-js.txt",
			"each":  "string",
			"copy":  "TestStatic/js/moved.js.gz",
			"minjs": null,
			"gzip":  null
		},
        {
            "list":   "TestStatic/img/oauth2-icons/*.svg",
			"sprite": "TestStatic/img/oauth2-icons.svg.gz",
			"gzip":   null
        }
	]
}
```

The configuration has two main sections:

1. `use`: Defines available steps in format: `"step_name": "BuilderClassName"`
1. `pipelines`: Defines pipelines and their steps in format: `"step_name": <config_data>|null`

## Built-in Pipeline Stages

1. **`Dm.Web.BuildStatic.Services.Stages.FilesSource`** - enumerates files in a folder. Requires a `"folder+pattern"` parameter (e.g., `"js/*.js"` for all JS files in the `js` folder, `"js/**/*.js"` for JS files in the `js` folder and its subfolders).
2. **`Dm.Web.BuildStatic.Services.Stages.ForEach`** - executes subsequent stages for each element in a collection. Requires a `"type-of-element"` parameter (e.g., `"string"`).
3. **`Dm.Web.BuildStatic.Services.Stages.FileCopy`** - copies a file from `src` (provided by the previous stage) to `dst` (required parameter).
4. **`Dm.Web.BuildStatic.Services.Stages.MinifyJS`** - minifies a JS file using Uglify. Accepts an `InOutStreams` model with source JS and outputs `InOutStreams` with minified JS in `InOutStreams.In`. If this is the final stage, saves the minified content to `InOutStreams.Out`.
5. **`Dm.Web.BuildStatic.Services.Stages.GzipStream`** - compresses the incoming `InOutStreams.In` stream to `InOutStreams.Out` using GZIP.
6. **`Dm.Web.BuildStatic.Services.Stages.SVGSprite`** - combines an incoming collection of SVG symbol files into a sprite. Requires a `dst` parameter (output destination).
