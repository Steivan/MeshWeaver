import { map, Observable, Subscription } from "rxjs";
import { Workspace } from "@open-smc/data/src/Workspace";
import { Collection } from "@open-smc/data/src/contract/Collection";
import { keys } from "lodash-es";
import { appStore } from "./appStore";
import { removeArea } from "./appReducer";
import { ControlRenderer } from "./ControlRenderer";
import { EntityReference } from "@open-smc/data/src/contract/EntityReference";
import { selectByReference } from "@open-smc/data/src/operators/selectByReference";

export class AreaCollectionRenderer {
    subscription = new Subscription();
    private state: Record<string, ControlRenderer> = {};

    constructor(areaReferences$: Observable<EntityReference[]>, collections: Workspace<Collection<Collection>>) {
        this.subscription.add(
            areaReferences$
                .subscribe(
                    references => {
                        references?.filter(reference => !this.state[reference.id])
                            .forEach(reference => {
                                const control$ = collections.pipe(map(selectByReference(reference)));
                                this.state[reference.id] =
                                    new ControlRenderer(control$, collections, reference.id);
                            });
                    }
                )
        );

        this.subscription.add(
            areaReferences$
                .subscribe(
                    references => {
                        keys(this.state).forEach(id => {
                            if (!references?.find(reference => reference.id === id)) {
                                appStore.dispatch(removeArea(id));
                                this.state[id].subscription.unsubscribe();
                                delete this.state[id];
                            }
                        })
                    }
                )
        );
    }
}