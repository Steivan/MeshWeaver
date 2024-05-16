import { Collection } from "@open-smc/data/src/contract/Collection";
import { EntityReference } from "@open-smc/data/src/contract/EntityReference";
import { Workspace } from "@open-smc/data/src/Workspace";
import { distinctUntilEqual } from "@open-smc/data/src/operators/distinctUntilEqual";
import { UiControl } from "@open-smc/layout/src/contract/controls/UiControl.ts";
import { map, Observable, of, Subscription, tap } from "rxjs";
import { updateStore } from "@open-smc/data/src/workspaceReducer";

export const uiControlType = (UiControl as any).$type;

export class LayoutViews extends Workspace<Collection<UiControl>> {
    subscription = new Subscription();

    constructor() {
        super({});
    }
    
    addView(area: string, control: UiControl | Observable<UiControl>) {
        if (!area) {
            area = this.getAreaName();
        }
        
        const control$ = control instanceof Observable ? control : of(control);

        this.subscription.add(
            control$
                .pipe(distinctUntilEqual())
                .pipe(
                    map(value => updateStore(state => {
                        state[area] = value;
                    }))
                )
                .subscribe(this)
        );
        return new EntityReference(uiControlType, area);
    }

    private getAreaName() {
        return `area_${this.n++}`;
    }

    private n = 0;
}