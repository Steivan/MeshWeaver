import { JsonPathReference, WorkspaceReference } from "./data.contract";
import { JSONPath } from "jsonpath-plus";

export const select = (value: any, reference: WorkspaceReference) => {
    if (reference instanceof JsonPathReference) {
        return JSONPath(
            {
                json: value,
                path: reference.path,
                wrap: false
            }
        );
    }
}