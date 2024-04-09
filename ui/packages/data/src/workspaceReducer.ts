import {
    createAction,
    createReducer
} from '@reduxjs/toolkit';
import { applyPatches, enablePatches, Patch } from "immer";
import { identity } from "lodash-es";
import { JsonPatch, PatchOperation } from "./contract/JsonPatch";

enablePatches();

export const patch = createAction<JsonPatch>('patch');
export const setState = createAction<unknown>('setState');

export const patchRequest = createAction<JsonPatch>('patchRequest');

export const workspaceReducer = createReducer(
    undefined,
    builder => {
        builder
            .addCase(
                patch,
                (state, action) =>
                    action.payload.operations &&
                    applyPatches(state, action.payload.operations.map(toImmerPatch))
            )
            .addCase(
                setState,
                (state, action) => action.payload
            );
    }
);

function toImmerPatch(patch: PatchOperation): Patch {
    const {op, path, value} = patch;

    return {
        op,
        path: path?.split("/").filter(identity),
        value
    }
}