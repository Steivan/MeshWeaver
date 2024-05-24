import { type } from "@open-smc/serialization/src/type";
import { WorkspaceReference } from "./WorkspaceReference";
import { EntityStore } from "./EntityStore";

@type("OpenSmc.Data.LayoutAreaReference")
export class LayoutAreaReference extends WorkspaceReference<EntityStore> {
    options: {}

    constructor(public area: string) {
        super();
    }
}