import * as process from 'process';
import { JsonRpcRequest, JsonRpcResponse, JsonRpcNotification, JsonRpcError, ErrorCodes } from './types';

type RequestHandler = (params: any) => Promise<any>;
type NotificationHandler = (params: any) => void;

/**
 * JSON-RPC 2.0 transport using LSP-style framing over stdin/stdout.
 * 
 * Message format:
 *   Content-Length: {byte_count}\r\n
 *   \r\n
 *   {json_payload}
 */
export class JsonRpcTransport {
  private requestHandlers: Map<string, RequestHandler> = new Map();
  private notificationHandlers: Map<string, NotificationHandler> = new Map();
  private buffer: Buffer = Buffer.alloc(0);
  private isReading = false;
  private nextMessageLength: number | null = null;

  /**
   * Register a handler for incoming JSON-RPC requests.
   * The handler receives params and should return a Promise with the result.
   */
  onRequest(method: string, handler: RequestHandler): void {
    this.requestHandlers.set(method, handler);
  }

  /**
   * Register a handler for incoming JSON-RPC notifications.
   * The handler receives params and should not return a value.
   */
  onNotification(method: string, handler: NotificationHandler): void {
    this.notificationHandlers.set(method, handler);
  }

  /**
   * Send a JSON-RPC notification to the host.
   */
  sendNotification(method: string, params?: any): void {
    const notification: JsonRpcNotification = {
      jsonrpc: '2.0',
      method,
      ...(params !== undefined && { params }),
    };
    this.writeMessage(notification);
  }

  /**
   * Send a JSON-RPC response to the host.
   */
  sendResponse(id: number, result?: any, error?: JsonRpcError): void {
    const response: JsonRpcResponse = {
      jsonrpc: '2.0',
      id,
      ...(error ? { error } : { result }),
    };
    this.writeMessage(response);
  }

  /**
   * Start reading from stdin and processing messages.
   */
  start(): void {
    if (this.isReading) {
      return;
    }

    this.isReading = true;
    process.stdin.on('data', (chunk: Buffer) => this.handleData(chunk));
    process.stdin.on('end', () => this.stop());
    process.stdin.resume();
  }

  /**
   * Stop reading from stdin and clean up.
   */
  stop(): void {
    if (!this.isReading) {
      return;
    }

    this.isReading = false;
    process.stdin.pause();
    process.stdin.removeAllListeners('data');
    process.stdin.removeAllListeners('end');
  }

  /**
   * Handle incoming data from stdin, buffering and parsing LSP-style messages.
   */
  private handleData(chunk: Buffer): void {
    // Append new data to buffer
    this.buffer = Buffer.concat([this.buffer, chunk]);

    // Process all complete messages in buffer
    while (this.processNextMessage()) {
      // Continue processing until no complete message remains
    }
  }

  /**
   * Process the next complete message from the buffer.
   * Returns true if a message was processed, false if incomplete.
   */
  private processNextMessage(): boolean {
    // If we don't know the message length yet, try to parse headers
    if (this.nextMessageLength === null) {
      const headerEnd = this.findHeaderEnd();
      if (headerEnd === -1) {
        return false; // Headers incomplete
      }

      const headerText = this.buffer.toString('utf8', 0, headerEnd);
      const length = this.parseContentLength(headerText);
      
      if (length === null) {
        // Malformed header - skip to end of headers and try next message
        console.warn('JSON-RPC: Malformed message header, skipping');
        this.buffer = this.buffer.subarray(headerEnd + 4); // Skip past \r\n\r\n
        return true; // Try next message
      }

      this.nextMessageLength = length;
      this.buffer = this.buffer.subarray(headerEnd + 4); // Remove headers
    }

    // Check if we have the complete JSON payload
    if (this.buffer.length < this.nextMessageLength) {
      return false; // Payload incomplete
    }

    // Extract and parse JSON payload
    const jsonBytes = this.buffer.subarray(0, this.nextMessageLength);
    this.buffer = this.buffer.subarray(this.nextMessageLength);
    this.nextMessageLength = null;

    try {
      const jsonText = jsonBytes.toString('utf8');
      const message = JSON.parse(jsonText);
      this.handleMessage(message);
    } catch (err) {
      console.warn('JSON-RPC: Failed to parse message:', err);
    }

    return true; // Continue processing
  }

  /**
   * Find the end of headers (\r\n\r\n) in the buffer.
   * Returns the index of the first \r, or -1 if not found.
   */
  private findHeaderEnd(): number {
    for (let i = 0; i < this.buffer.length - 3; i++) {
      if (
        this.buffer[i] === 0x0d &&     // \r
        this.buffer[i + 1] === 0x0a && // \n
        this.buffer[i + 2] === 0x0d && // \r
        this.buffer[i + 3] === 0x0a    // \n
      ) {
        return i;
      }
    }
    return -1;
  }

  /**
   * Parse Content-Length from header text (case-insensitive).
   * Returns null if Content-Length is not found or invalid.
   */
  private parseContentLength(headerText: string): number | null {
    const lines = headerText.split('\r\n');
    for (const line of lines) {
      const match = line.match(/^content-length:\s*(\d+)$/i);
      if (match) {
        const length = parseInt(match[1], 10);
        return isNaN(length) ? null : length;
      }
    }
    return null;
  }

  /**
   * Handle a parsed JSON-RPC message (request, response, or notification).
   */
  private handleMessage(message: any): void {
    if (!message || message.jsonrpc !== '2.0') {
      console.warn('JSON-RPC: Invalid message format');
      return;
    }

    // Check if it's a request (has id and method)
    if ('id' in message && 'method' in message) {
      this.handleRequest(message as JsonRpcRequest);
      return;
    }

    // Check if it's a notification (has method but no id)
    if ('method' in message && !('id' in message)) {
      this.handleNotificationMessage(message as JsonRpcNotification);
      return;
    }

    // Check if it's a response (has id but no method)
    if ('id' in message && !('method' in message)) {
      // Responses are typically handled by a request manager, not by this transport
      // For now, we just log it
      console.warn('JSON-RPC: Received unexpected response message');
      return;
    }

    console.warn('JSON-RPC: Unknown message type');
  }

  /**
   * Handle an incoming JSON-RPC request.
   */
  private async handleRequest(request: JsonRpcRequest): Promise<void> {
    const handler = this.requestHandlers.get(request.method);

    if (!handler) {
      this.sendResponse(request.id, undefined, {
        code: ErrorCodes.MethodNotFound,
        message: `Method not found: ${request.method}`,
      });
      return;
    }

    try {
      const result = await handler(request.params);
      this.sendResponse(request.id, result);
    } catch (err: any) {
      this.sendResponse(request.id, undefined, {
        code: ErrorCodes.InternalError,
        message: err?.message || 'Internal error',
        data: err?.stack,
      });
    }
  }

  /**
   * Handle an incoming JSON-RPC notification.
   */
  private handleNotificationMessage(notification: JsonRpcNotification): void {
    const handler = this.notificationHandlers.get(notification.method);

    if (!handler) {
      console.warn(`JSON-RPC: No handler for notification: ${notification.method}`);
      return;
    }

    try {
      handler(notification.params);
    } catch (err) {
      console.warn(`JSON-RPC: Error handling notification ${notification.method}:`, err);
    }
  }

  /**
   * Write a JSON-RPC message to stdout with LSP-style framing.
   */
  private writeMessage(message: JsonRpcRequest | JsonRpcResponse | JsonRpcNotification): void {
    const jsonText = JSON.stringify(message);
    const jsonBytes = Buffer.from(jsonText, 'utf8');
    const header = `Content-Length: ${jsonBytes.length}\r\n\r\n`;
    const headerBytes = Buffer.from(header, 'utf8');

    const fullMessage = Buffer.concat([headerBytes, jsonBytes]);
    process.stdout.write(fullMessage);
  }
}
