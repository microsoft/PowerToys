/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { RunOnceScheduler, createCancelablePromise } from '../../../base/common/async.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
export class HoverOperation {
    constructor(computer, success, error, progress, hoverTime) {
        this._computer = computer;
        this._state = 0 /* IDLE */;
        this._hoverTime = hoverTime;
        this._firstWaitScheduler = new RunOnceScheduler(() => this._triggerAsyncComputation(), 0);
        this._secondWaitScheduler = new RunOnceScheduler(() => this._triggerSyncComputation(), 0);
        this._loadingMessageScheduler = new RunOnceScheduler(() => this._showLoadingMessage(), 0);
        this._asyncComputationPromise = null;
        this._asyncComputationPromiseDone = false;
        this._completeCallback = success;
        this._errorCallback = error;
        this._progressCallback = progress;
    }
    setHoverTime(hoverTime) {
        this._hoverTime = hoverTime;
    }
    _firstWaitTime() {
        return this._hoverTime / 2;
    }
    _secondWaitTime() {
        return this._hoverTime / 2;
    }
    _loadingMessageTime() {
        return 3 * this._hoverTime;
    }
    _triggerAsyncComputation() {
        this._state = 2 /* SECOND_WAIT */;
        this._secondWaitScheduler.schedule(this._secondWaitTime());
        if (this._computer.computeAsync) {
            this._asyncComputationPromiseDone = false;
            this._asyncComputationPromise = createCancelablePromise(token => this._computer.computeAsync(token));
            this._asyncComputationPromise.then((asyncResult) => {
                this._asyncComputationPromiseDone = true;
                this._withAsyncResult(asyncResult);
            }, (e) => this._onError(e));
        }
        else {
            this._asyncComputationPromiseDone = true;
        }
    }
    _triggerSyncComputation() {
        if (this._computer.computeSync) {
            this._computer.onResult(this._computer.computeSync(), true);
        }
        if (this._asyncComputationPromiseDone) {
            this._state = 0 /* IDLE */;
            this._onComplete(this._computer.getResult());
        }
        else {
            this._state = 3 /* WAITING_FOR_ASYNC_COMPUTATION */;
            this._onProgress(this._computer.getResult());
        }
    }
    _showLoadingMessage() {
        if (this._state === 3 /* WAITING_FOR_ASYNC_COMPUTATION */) {
            this._onProgress(this._computer.getResultWithLoadingMessage());
        }
    }
    _withAsyncResult(asyncResult) {
        if (asyncResult) {
            this._computer.onResult(asyncResult, false);
        }
        if (this._state === 3 /* WAITING_FOR_ASYNC_COMPUTATION */) {
            this._state = 0 /* IDLE */;
            this._onComplete(this._computer.getResult());
        }
    }
    _onComplete(value) {
        this._completeCallback(value);
    }
    _onError(error) {
        if (this._errorCallback) {
            this._errorCallback(error);
        }
        else {
            onUnexpectedError(error);
        }
    }
    _onProgress(value) {
        this._progressCallback(value);
    }
    start(mode) {
        if (mode === 0 /* Delayed */) {
            if (this._state === 0 /* IDLE */) {
                this._state = 1 /* FIRST_WAIT */;
                this._firstWaitScheduler.schedule(this._firstWaitTime());
                this._loadingMessageScheduler.schedule(this._loadingMessageTime());
            }
        }
        else {
            switch (this._state) {
                case 0 /* IDLE */:
                    this._triggerAsyncComputation();
                    this._secondWaitScheduler.cancel();
                    this._triggerSyncComputation();
                    break;
                case 2 /* SECOND_WAIT */:
                    this._secondWaitScheduler.cancel();
                    this._triggerSyncComputation();
                    break;
            }
        }
    }
    cancel() {
        this._loadingMessageScheduler.cancel();
        if (this._state === 1 /* FIRST_WAIT */) {
            this._firstWaitScheduler.cancel();
        }
        if (this._state === 2 /* SECOND_WAIT */) {
            this._secondWaitScheduler.cancel();
            if (this._asyncComputationPromise) {
                this._asyncComputationPromise.cancel();
                this._asyncComputationPromise = null;
            }
        }
        if (this._state === 3 /* WAITING_FOR_ASYNC_COMPUTATION */) {
            if (this._asyncComputationPromise) {
                this._asyncComputationPromise.cancel();
                this._asyncComputationPromise = null;
            }
        }
        this._state = 0 /* IDLE */;
    }
}
