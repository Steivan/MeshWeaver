import { UiControl } from "@open-smc/layout/src/contract/controls/UiControl";
import { combineLatest, distinctUntilChanged, map, merge, Observable, skip, Subscription, switchMap } from "rxjs";
import { Workspace } from "@open-smc/data/src/Workspace";
import { Collection } from "@open-smc/data/src/contract/Collection";
import { EntityStore } from "@open-smc/data/src/contract/EntityStore";
import { sliceByPath } from "@open-smc/data/src/sliceByPath";
import { selectByPath } from "@open-smc/data/src/operators/selectByPath";
import { effect } from "@open-smc/utils/src/operators/effect";
import { keys, omit, pickBy, toPairs } from "lodash-es";
import { distinctUntilEqual } from "@open-smc/data/src/operators/distinctUntilEqual";
import { sliceByReference } from "@open-smc/data/src/sliceByReference";
import { expandBindings } from "./expandBindings";
import { removeArea, setArea, setRoot } from "./appReducer";
import { Binding, isBinding } from "@open-smc/layout/src/contract/Binding";
import { app$, appStore } from "./appStore";
import { bindingToPatchAction } from "./bindingToPatchAction";
import { LayoutStackControl } from "@open-smc/layout/src/contract/controls/LayoutStackControl";
import { cloneDeepWith } from "lodash";
import { EntityReference } from "@open-smc/data/src/contract/EntityReference";

const uiControlType = (UiControl as any).$type;

export class EntityStoreRenderer {
    readonly subscription = new Subscription();

    private collectionsWorkspace: Workspace<Collection<Collection>>;
    private controls$: Observable<Collection<UiControl>>;

    constructor(private entityStore: Workspace<EntityStore>) {
        const collectionsWorkspace = this.collectionsWorkspace =
            sliceByPath<EntityStore, Collection<Collection>>(entityStore, "/collections");

        this.controls$ = collectionsWorkspace.pipe(map(selectByPath(`/${uiControlType}`)));

        const rootArea$ = entityStore.pipe(map(selectByPath<string>("/reference/area")));

        this.subscription.add(collectionsWorkspace.subscription);
        this.subscription.add(
            rootArea$
                .pipe(distinctUntilChanged())
                .pipe(
                    effect(
                        rootArea => {
                            const control$ =
                                this.controls$.pipe(
                                    map(selectByPath(`/${rootArea}`))
                                )

                            return this.renderControl(rootArea, control$);
                        }
                    )
                )
                .subscribe(rootArea => {
                    appStore.dispatch(setRoot(rootArea));
                })
        );
    }

    private renderControl(area: string, control$: Observable<UiControl>, parentDataContext?: unknown) {
        const state: Record<string, Subscription> = {};

        const subscription = new Subscription();

        const nestedAreas$ =
            control$
                .pipe(map(this.nestedAreas))
                .pipe(distinctUntilChanged());

        subscription.add(
            nestedAreas$
                .subscribe(
                    references => {
                        references?.filter(reference => !state[reference.id])
                            .forEach(reference => {
                                state[reference.id] = this.renderControl(
                                    reference.id,
                                    this.controls$.pipe(
                                        map(controls => controls?.[reference.id])
                                    )
                                );
                            });
                    }
                )
        );

        const dataContext$ =
            control$
                .pipe(map(control => control?.dataContext))
                .pipe(distinctUntilChanged());

        const props$ =
            control$
                .pipe(map(control => control && omit(control, 'dataContext')))
                .pipe(distinctUntilEqual());

        subscription.add(
            dataContext$
                .pipe(
                    effect(
                        dataContext => {
                            const subscription = new Subscription();

                            const dataContextWorkspace =
                                sliceByReference(this.collectionsWorkspace, dataContext, area);

                            const setArea$ =
                                combineLatest([dataContextWorkspace, control$.pipe(distinctUntilChanged())])
                                    .pipe(
                                        map(
                                            ([dataContextState, control]) => {
                                                if (control) {
                                                    const componentTypeName = control.constructor.name;
                                                    const {dataContext, ...props} = control;
                                                    const boundProps =
                                                        expandBindings(nestedAreasToIds(props), parentDataContext)(dataContextState);

                                                    return setArea({
                                                        id: area,
                                                        control: {
                                                            componentTypeName,
                                                            props: boundProps
                                                        }
                                                    });
                                                }
                                                else {
                                                    return setArea({
                                                        id: area
                                                    })
                                                }
                                            }
                                        )
                                    );

                            const dataContextPatch$ =
                                props$
                                    .pipe(
                                        switchMap(
                                            props => {
                                                const bindings: Record<string, Binding> = pickBy(props, isBinding);

                                                return merge(
                                                    ...toPairs(bindings)
                                                        .map(
                                                            ([key, binding]) =>
                                                                app$
                                                                    .pipe(map(appState => appState.areas[area].control.props[key]))
                                                                    .pipe(distinctUntilChanged())
                                                                    .pipe(skip(1))
                                                                    .pipe(map(bindingToPatchAction(binding)))
                                                        )
                                                );
                                            })
                                    );

                            subscription.add(dataContextWorkspace.subscription);
                            subscription.add(setArea$.subscribe(appStore.dispatch));
                            subscription.add(dataContextPatch$.subscribe(dataContextWorkspace));

                            return subscription;
                        }
                    )
                )
                .subscribe()
        )

        subscription.add(
            nestedAreas$
                .subscribe(
                    references => {
                        keys(state).forEach(id => {
                            if (!references?.find(area => area.id === id)) {
                                appStore.dispatch(removeArea(id));
                                state[id].unsubscribe();
                                delete state[id];
                            }
                        })
                    }
                )
        );

        return subscription;
    }

    private nestedAreas(control: UiControl) {
        if (control instanceof LayoutStackControl) {
            return control?.areas;
        }
    }
}

const nestedAreasToIds = <T>(props: T): T =>
    cloneDeepWith(
        props,
        value => value instanceof EntityReference
            ? value.id : undefined
    );