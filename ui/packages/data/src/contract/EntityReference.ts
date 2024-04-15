import { type } from "@open-smc/serialization/src/type";
import { PathReferenceBase } from "./PathReferenceBase";

@type("OpenSmc.Data.EntityReference")
export class EntityReference<T = unknown> extends PathReferenceBase<T> {
    constructor(public collection: string, public id: string) {
        super();
    }

    protected get path() {
        return `/${this.collection}/${this.id}`;
    }
}