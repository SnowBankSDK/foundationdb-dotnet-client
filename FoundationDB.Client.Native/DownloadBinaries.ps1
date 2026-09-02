param (
	[string]$version = "latest",
	[string]$manifestPath = "./manifest.json",
	[string]$outputDir = ".",
	[switch]$full,
	[switch]$offline,
	[switch]$force
)

# Ensure .NET HttpClient support
Add-Type -AssemblyName System.Net.Http

# Load the manifest
$manifest = Get-Content $manifestPath | ConvertFrom-Json

if ([string]::IsNullOrEmpty($version) -or $version -eq "latest") {
	$version = $manifest.latest
}

# Check if the specified version exists in the manifest
if (-not ($manifest.versions.PSObject.Properties.Name -contains $version)) {
	Write-Error "Specified version '${version}' does not exist in the manifest."
	exit 1
}

# Get the files for the specified version
$files = $manifest.versions.$version.files

# Only keep 'fdbcli' entries when '-Full'' is specified
if (-not $Full) {
	$files = $files | Where-Object { $_.name -notmatch "fdbcli" }
}

Write-Host "Downloading $($files.Count) files for ${version}:" -ForegroundColor White

# Initialize HttpClient
$httpClient = [System.Net.Http.HttpClient]::new()

function DownloadFile {
	param (
		[string]$uri,
		[string]$output,
		[string]$name
	)

	try {
		# Get the absolute path, otherwise File::Create(...) will not resolve '.' correctly!
		$output = (Join-Path -Path $PWD.Path -ChildPath $output)

		# GET ...
		$request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $uri)
		$response = $httpClient.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
		$stream = $response.Content.ReadAsStreamAsync().Result

		# create file
		$fileStream = [System.IO.File]::Create($output)

		$buffer = New-Object byte[] 1048576
		$totalBytes = $response.Content.Headers.ContentLength
		$downloadedBytes = 0
		$lastUpdateTime = Get-Date

		while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
			$fileStream.Write($buffer, 0, $read)
			$downloadedBytes += $read

			# Calculate progress percentage
			$progressPercent = [math]::Round(($downloadedBytes / $totalBytes) * 100)

			# Check if we should update progress (either time-based or percentage-based)
			$currentTime = Get-Date
			$timeSinceLastUpdate = ($currentTime - $lastUpdateTime).TotalSeconds

			if ($progressPercent -ge 100) {
				$formattedTotalBytes = $totalBytes.ToString("N0", [System.Globalization.CultureInfo]::CurrentCulture)
				Write-Progress -Activity "Downloading: $name" -PercentComplete $progressPercent -Status "$formattedTotalBytes bytes" -Completed
			} elseif ($timeSinceLastUpdate -ge 0.2) {
				$formattedBytes = $downloadedBytes.ToString("N0", [System.Globalization.CultureInfo]::CurrentCulture)
				$formattedTotalBytes = $totalBytes.ToString("N0", [System.Globalization.CultureInfo]::CurrentCulture)
				Write-Progress -Activity "Downloading: $name" -PercentComplete $progressPercent -Status "$formattedBytes / $formattedTotalBytes bytes"
				$lastUpdateTime = $currentTime
			}
		}
	}
	catch {
		Write-Host "`nDownload interrupted! Cleaning up..." -ForegroundColor Red

		# Show detailed error message for debugging
		Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
		Write-Host "Error Type: $($_.Exception.GetType().Name)" -ForegroundColor Yellow
		Write-Host "Stack Trace:" -ForegroundColor DarkGray
		Write-Host $_.ScriptStackTrace

		# Ensure partial file is deleted if canceled
		if (Test-Path -Path $output) {
			Remove-Item -Path $output -Force
			Write-Host "Partial file removed: $output" -ForegroundColor Yellow
		}
		
		Write-Host "Process terminated." -ForegroundColor Red
		exit 1
	}
	finally
	{
		if ($stream) { $stream.Close() }
		if ($fileStream) { $fileStream.Close() }
	}
}

foreach ($file in $files) {
	$name = $file.name
	$fileUrl = $file.url
	$fileChecksum = $file.checksum
	$rid = $file.rid

	# Where should we store the file ?
	$fileTargetDir = Join-Path -Path $outputDir -ChildPath (Join-Path -Path "runtimes" -ChildPath (Join-Path -Path $rid -ChildPath "native"))
	$fileTargetPath = Join-Path -Path $fileTargetDir -ChildPath $name

	# Ensure the path exists
	if (-not (Test-Path -Path $fileTargetDir)) {
		New-Item -ItemType Directory -Path $fileTargetDir | Out-Null
	}

	Write-Host ""
	Write-Host "- ${name} (${rid})" -ForegroundColor Yellow
	Write-Host "  - target   : " -ForegroundColor DarkGray -NoNewLine; Write-Host $fileTargetPath
	Write-Host "  - url      : " -ForegroundColor DarkGray -NoNewLine; Write-Host $fileUrl
	Write-Host "  - checksum : " -ForegroundColor DarkGray -NoNewLine; Write-Host $fileChecksum.ToLower()

	# If we already have the file (with the correct checksum), skip it
	if (Test-Path -Path $fileTargetPath) {
		$computedChecksum = Get-FileHash $fileTargetPath -Algorithm SHA256 | Select-Object -ExpandProperty Hash
		Write-Host "  - local    : " -ForegroundColor DarkGray -NoNewLine;
		if ($fileChecksum -eq $computedChecksum) {
			Write-Host $computedChecksum.ToLower()
			if ($force) {
				Write-Host "  => File $fileTargetPath exists and as the correct checksum, but will be downloaded anyways because -force was specified." -ForegroundColor Yellow
			} else {
				Write-Host "  => CACHED" -ForegroundColor Green
				continue
			}
		} else {
			Write-Host $computedChecksum.ToLower() -ForegroundColor Yellow
			Write-Host "  => File $fileTargetPath exists but has an incorrect checksum. Re-downloading." -ForegroundColor Red
		}
	}

	if ($offline) {
		Write-Host "  => SKIPPING DOWNLOAD (Offline Mode)" -ForegroundColor Cyan
		continue
	}

	# Download the file
	#Invoke-WebRequest -Uri $fileUrl -OutFile $fileTargetPath
	DownloadFile -uri $fileUrl -output $fileTargetPath -name $name

	# Verify the checksum
	$computedChecksum = Get-FileHash $fileTargetPath -Algorithm SHA256 | Select-Object -ExpandProperty Hash

	Write-Host "  - updated  : " -ForegroundColor DarkGray -NoNewLine;
	Write-Host $computedChecksum.ToLower()

	if ($fileChecksum -ne $computedChecksum) {
		Write-Error "Checksum verification failed for ${fileTargetPath}!"
		exit 1
	}

	Write-Host "  => OK" -ForegroundColor Green

}

# Record the version runtimes\ now holds. The csproj refuses to pack when this stamp differs from the package version.
if (-not $offline) {
	$stampDir = Join-Path -Path $outputDir -ChildPath "runtimes"
	if (-not (Test-Path -Path $stampDir)) { New-Item -ItemType Directory -Path $stampDir | Out-Null }
	$stampPath = Join-Path -Path (Resolve-Path -Path $stampDir).Path -ChildPath "fdb-native-version.txt"
	[System.IO.File]::WriteAllText($stampPath, "$version`n")
	Write-Host "Stamped runtimes\fdb-native-version.txt = $version" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Download complete." -ForegroundColor Green
