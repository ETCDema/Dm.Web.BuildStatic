 
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
    "parallel": true,
    "use": {
        "list":     "Dm.Web.BuildStatic.Services.Stages.FilesSource",
        "eachFile": "Dm.Web.BuildStatic.Services.Stages.EachFileCopy",
        "minjs":    "Dm.Web.BuildStatic.Services.Stages.MinifyJS",
        "gzip":     "Dm.Web.BuildStatic.Services.Stages.GzipStream",
		"sprite":   "Dm.Web.BuildStatic.Services.Stages.SVGSprite"
    },
    "pipelines": [
        {
            "list":  "TestStatic/js/**/*.js",
            "list":  "TestStatic/js/**/*.mjs",
            "eachFile": {
                "@dst":  "{path}.gz",
                "gzip":  "Optimal",
                "minjs": null
            }
        },
        {
            "list": "TestStatic/css/*.css",
            "eachFile": {
                "@dst": "{path}.gz",
                "gzip": null
            }
        },
        {
            "list":  "TestStatic/move-to-js.txt",
            "eachFile":  {
                "@dst":  "TestStatic/js/moved.js.gz",
                "gzip":  null,
                "minjs": null
            }
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

1. `use`: Defines available steps in format: `"step_name": "ClassName"`
1. `pipelines`: Defines pipelines and their steps in format: `"step_name": <config_data>|null`

Optional parameter:

1. `parallel`: Run the pipelines in parallel or sequentially.

## Built-in Pipeline Stages

1. **`Dm.Web.BuildStatic.Services.Stages.FilesSource`** - enumerates files in a folder. Requires a `"folder+pattern"` parameter (e.g., `"js/*.js"` for all JS files in the `js` folder, `"js/**/*.js"` for JS files in the `js` folder and its subfolders).
2. **`Dm.Web.BuildStatic.Services.Stages.EachFileCopy`** - copies a files from `src` collection (provided by the previous stage) to `@dst` (required parameter).
3. **`Dm.Web.BuildStatic.Services.Stages.FileCopy`** - copies a one file from `src` (provided by the previous stage) to `@dst` (required parameter).
4. **`Dm.Web.BuildStatic.Services.Stages.MinifyJS`** - minifies a JS file using Uglify. Accepts an `InOutStreams` model with source JS and outputs `InOutStreams` with minified JS in `InOutStreams.In`. If this is the final stage, saves the minified content to `InOutStreams.Out`.
5. **`Dm.Web.BuildStatic.Services.Stages.GzipStream`** - compresses the incoming `InOutStreams.In` stream to `InOutStreams.Out` using GZIP.
6. **`Dm.Web.BuildStatic.Services.Stages.SVGSprite`** - combines an incoming collection of SVG symbol files into a sprite. Requires a `dst` parameter (output destination).
