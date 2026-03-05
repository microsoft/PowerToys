// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ExtensionServer } from '@cmdpal/sdk';
import { SamplePagesProvider } from './provider';

// Register and start the extension
const provider = new SamplePagesProvider();
ExtensionServer.register(provider);
ExtensionServer.start();
