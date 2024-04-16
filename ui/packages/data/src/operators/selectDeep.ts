import { cloneDeepWith, get } from "lodash-es";
import { ValueOrReference } from "../contract/ValueOrReference";
import { WorkspaceReference } from "../contract/WorkspaceReference";
import { selectByReference } from "./selectByReference";

export const selectDeep = <T>(data: ValueOrReference<T>) =>
    (source: unknown): T =>
        cloneDeepWith(
            data,
            value =>
                value instanceof WorkspaceReference
                    ? (selectByReference(value)(source) ?? null)
                    : undefined
        );

function select(path: string) {
        return <T>(source: T) => get(source, path)
}