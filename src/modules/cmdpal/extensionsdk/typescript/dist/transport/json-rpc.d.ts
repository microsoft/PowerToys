import { JsonRpcError } from './types';
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
export declare class JsonRpcTransport {
    private requestHandlers;
    private notificationHandlers;
    private buffer;
    private isReading;
    private nextMessageLength;
    /**
     * Register a handler for incoming JSON-RPC requests.
     * The handler receives params and should return a Promise with the result.
     */
    onRequest(method: string, handler: RequestHandler): void;
    /**
     * Register a handler for incoming JSON-RPC notifications.
     * The handler receives params and should not return a value.
     */
    onNotification(method: string, handler: NotificationHandler): void;
    /**
     * Send a JSON-RPC notification to the host.
     */
    sendNotification(method: string, params?: any): void;
    /**
     * Send a JSON-RPC response to the host.
     */
    sendResponse(id: number, result?: any, error?: JsonRpcError): void;
    /**
     * Start reading from stdin and processing messages.
     */
    start(): void;
    /**
     * Stop reading from stdin and clean up.
     */
    stop(): void;
    /**
     * Handle incoming data from stdin, buffering and parsing LSP-style messages.
     */
    private handleData;
    /**
     * Process the next complete message from the buffer.
     * Returns true if a message was processed, false if incomplete.
     */
    private processNextMessage;
    /**
     * Find the end of headers (\r\n\r\n) in the buffer.
     * Returns the index of the first \r, or -1 if not found.
     */
    private findHeaderEnd;
    /**
     * Parse Content-Length from header text (case-insensitive).
     * Returns null if Content-Length is not found or invalid.
     */
    private parseContentLength;
    /**
     * Handle a parsed JSON-RPC message (request, response, or notification).
     */
    private handleMessage;
    /**
     * Handle an incoming JSON-RPC request.
     */
    private handleRequest;
    /**
     * Handle an incoming JSON-RPC notification.
     */
    private handleNotificationMessage;
    /**
     * Write a JSON-RPC message to stdout with LSP-style framing.
     */
    private writeMessage;
}
export {};
//# sourceMappingURL=json-rpc.d.ts.map