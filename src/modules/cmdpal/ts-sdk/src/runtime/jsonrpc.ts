// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Minimal JSON-RPC 2.0 message shapes and guards for the Command Palette
 * transport. See `03-jsonrpc-protocol.md`.
 */

export const JSONRPC_VERSION = '2.0';

/** Standard JSON-RPC 2.0 error codes used by the runtime. */
export const JsonRpcErrorCode = {
  ParseError: -32700,
  InvalidRequest: -32600,
  MethodNotFound: -32601,
  InvalidParams: -32602,
  InternalError: -32603,
} as const;

export interface JsonRpcRequest {
  jsonrpc: typeof JSONRPC_VERSION;
  id: number | string;
  method: string;
  params?: unknown;
}

export interface JsonRpcNotification {
  jsonrpc: typeof JSONRPC_VERSION;
  method: string;
  params?: unknown;
}

export interface JsonRpcError {
  code: number;
  message: string;
  data?: unknown;
}

export interface JsonRpcResponse {
  jsonrpc: typeof JSONRPC_VERSION;
  id: number | string;
  result?: unknown;
  error?: JsonRpcError;
}

export type JsonRpcMessage = JsonRpcRequest | JsonRpcNotification | JsonRpcResponse;

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/** Narrows a parsed value to a JSON-RPC request (has both `id` and `method`). */
export function isRequest(message: unknown): message is JsonRpcRequest {
  if (!isObject(message)) {
    return false;
  }
  const hasId = typeof message.id === 'number' || typeof message.id === 'string';
  return hasId && typeof message.method === 'string';
}

/** Narrows a parsed value to a JSON-RPC notification (has `method`, no `id`). */
export function isNotification(message: unknown): message is JsonRpcNotification {
  if (!isObject(message)) {
    return false;
  }
  return message.id === undefined && typeof message.method === 'string';
}

/** Reads `params` as a string-keyed record, or an empty record when absent. */
export function asParams(params: unknown): Record<string, unknown> {
  return isObject(params) ? params : {};
}

/** Reads a string field from a params record, or `undefined` when not a string. */
export function stringField(params: Record<string, unknown>, key: string): string | undefined {
  const value = params[key];
  return typeof value === 'string' ? value : undefined;
}
