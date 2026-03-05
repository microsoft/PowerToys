// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  IMarkdownContent,
  IFormContent,
  ITreeContent,
  IContent,
  ICommandResult,
  ContentType,
} from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';

/**
 * Markdown content for displaying rich text.
 */
export class MarkdownContent implements IMarkdownContent {
  type = ContentType.Markdown;
  id: string = '';
  body: string = '';

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;

  constructor(body?: string) {
    if (body !== undefined) {
      this.body = body;
    }
  }

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      this._transport.sendNotification('content/propChanged', {
        contentId: this.id,
        propertyName,
      });
    }
  }
}

/**
 * Form content for user input.
 */
export abstract class FormContent implements IFormContent {
  type = ContentType.Form;
  id: string = '';

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;

  abstract get templateJson(): string;
  abstract get dataJson(): string;
  abstract get stateJson(): string;

  abstract submitForm(inputs: string, data: string): ICommandResult;

  /** Ensure getter-based properties are included in JSON serialization. */
  toJSON() {
    return {
      type: this.type,
      id: this.id,
      templateJson: this.templateJson,
      dataJson: this.dataJson,
      stateJson: this.stateJson,
    };
  }

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      this._transport.sendNotification('content/propChanged', {
        contentId: this.id,
        propertyName,
      });
    }
  }
}

/**
 * Tree content for hierarchical data.
 */
export abstract class TreeContent implements ITreeContent {
  type = ContentType.Tree;
  id: string = '';

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;
  ItemsChanged?: (args: unknown) => void;

  /** Get the child content nodes. */
  abstract getChildren(): IContent[];

  /** Ensure children are included in JSON serialization. */
  toJSON() {
    return {
      type: this.type,
      id: this.id,
      children: this.getChildren(),
    };
  }

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      this._transport.sendNotification('content/propChanged', {
        contentId: this.id,
        propertyName,
      });
    }
  }

  protected notifyItemsChanged(totalItems?: number): void {
    if (this.ItemsChanged) {
      this.ItemsChanged({ totalItems: totalItems ?? -1 });
    }
  }
}
