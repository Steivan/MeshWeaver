import { UiControl } from "@open-smc/layout/src/contract/controls/UiControl";
import { cloneDeepWith, omit } from "lodash-es";
import { ValueOrReference } from "@open-smc/data/src/contract/ValueOrReference";
import { Binding, ValueOrBinding } from "@open-smc/data/src/contract/Binding";
import { JsonPathReference } from "@open-smc/data/src/contract/JsonPathReference";
import { Workspace } from "@open-smc/data/src/Workspace";
import { app$, appStore, ControlModel } from "./appStore";
import { distinctUntilChanged, map, Observable, Subscription } from "rxjs";
import { effect } from "@open-smc/utils/src/operators/effect";
import { syncWorkspaces } from "./syncWorkspaces";
import { sliceByReference } from "@open-smc/data/src/sliceByReference";
import { distinctUntilEqual } from "@open-smc/data/src/operators/distinctUntilEqual";
import { setArea } from "./appReducer";
import { pathToUpdateAction } from "@open-smc/data/src/operators/pathToUpdateAction";
import { Renderer } from "./Renderer";
import { RendererStackTrace } from "./RendererStackTrace";

export class ControlRenderer<T extends UiControl = UiControl> extends Renderer {
    readonly subscription = new Subscription();
    readonly namespace: string;

    constructor(
        public readonly control$: Observable<T>,
        public readonly area: string,
        stackTrace: RendererStackTrace
    ) {
        super(new Workspace(null, `${area}/dataContext`), stackTrace);

        this.subscription.add(
            this.control$
                .pipe(map(control => control?.dataContext))
                .pipe(distinctUntilChanged())
                .pipe(
                    effect(
                        dataContext =>
                            syncWorkspaces(
                                dataContext ? sliceByReference(this.rootContext, dataContext)
                                    : this.parentContext,
                                this.dataContext
                            )
                    )
                )
                .subscribe()
        );

        this.render();
    }

    protected render() {
        this.subscription.add(
            this.control$
                .pipe(distinctUntilEqual())
                .pipe(
                    effect(
                        control => {
                            if (control) {
                                const controlModel =
                                    this.getModel(control);

                                const controlModelWorkspace =
                                    sliceByReference(this.dataContext, controlModel);

                                return this.renderControlTo(controlModelWorkspace);
                            }
                        }
                    )
                )
                .subscribe()
        );
    }

    protected getModel(control: T): ControlModel {
        if (control) {
            const componentTypeName = control.constructor.name;
            const props = bindingsToReferences(
                extractProps(control)
            );

            return {
                componentTypeName,
                props
            }
        }
    }

    protected renderControlTo(controlModelWorkspace: Workspace<ControlModel>)  {
        const subscription = new Subscription();

        const area = this.area;

        subscription.add(
            controlModelWorkspace
                .pipe(distinctUntilEqual())
                .subscribe(control => {
                    appStore.dispatch(setArea({
                        area,
                        control
                    }))
                })
        );

        subscription.add(
            app$
                .pipe(map(appState => appState.areas[area]?.control))
                .pipe(distinctUntilChanged())
                .pipe(map(pathToUpdateAction("")))
                .subscribe(controlModelWorkspace)
        );

        return subscription;
    }
}

export const extractProps = (control: UiControl) =>
    omit(control, 'dataContext');

export const bindingsToReferences = <T>(props: ValueOrBinding<T>): ValueOrReference<T> =>
    cloneDeepWith(
        props,
        value =>
            value instanceof Binding
                ? new JsonPathReference(value.path) : undefined
    );
