<#
	A little Hello all planets PowerShell script

	To run, open PowerShell and enter 
	
	& '.\PowerShell script.ps1' 

	You can do this by entering P and the hit the TAB key followed by ENTER
#>


# All planets sorted by distance from the sun
$all_planets = @("Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune")


# Define a function hello_all_planets, which iterates over all_planets 
$hello_all_planets = {
	foreach ($current_planet in $all_planets) { 
		# Write Hello, Mercury! etc. 
		Write-Host "Hello, $($current_planet)!"
	}
}

<#
	Call the hello_all_planets function that we defined above. 
	
	Note: If this line &$hello_all_planets wasn't there nothing would happen, eventhough we defined the function.
#>
&$hello_all_planets
