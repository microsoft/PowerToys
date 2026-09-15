# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param([Parameter(Mandatory = $true)][string] $FilePath)

Get-Content -LiteralPath $FilePath -ErrorAction Stop
