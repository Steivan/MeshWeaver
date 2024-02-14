var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => {
  __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
  return value;
};
import { Observable, Subject, filter, map, Subscription } from "rxjs";
import { methodName, uiAddress, layoutAddress } from "./contract.mjs";
import fs from "fs";
class WebSocketClientHub extends Observable {
  constructor(webSocketClient, webSocket) {
    super((subscriber) => {
      const handler = (data, client) => client === webSocketClient && subscriber.next(data);
      webSocket.on(methodName, handler);
      subscriber.add(() => webSocket.off(methodName, handler));
    });
    this.webSocketClient = webSocketClient;
  }
  complete() {
  }
  error(err) {
  }
  next(value) {
    this.webSocketClient.send(methodName, value);
  }
}
class SubjectHub extends Observable {
  constructor(input = new Subject(), output = new Subject()) {
    super((subscriber) => output.subscribe(subscriber));
    this.input = input;
    this.output = output;
  }
  complete() {
  }
  error(err) {
  }
  next(value) {
    this.input.next(value);
  }
}
function makeProxy() {
  const subject1 = new Subject();
  const subject2 = new Subject();
  const hub1 = new SubjectHub(subject1, subject2);
  const hub2 = new SubjectHub(subject2, subject1);
  return [hub1, hub2];
}
function connect(hub1, hub2) {
  const subscription = hub1.subscribe(hub2);
  subscription.add(hub2.subscribe(hub1));
  return subscription;
}
function filterByTarget(target) {
  return filter((envelope) => envelope.target === target);
}
function addSender(sender) {
  return map((envelope) => {
    return {
      ...envelope,
      sender
    };
  });
}
function ofType(ctor) {
  return filter((envelope) => envelope.message instanceof ctor);
}
function sendMessage(observer, message, envelope) {
  observer.next({
    ...envelope ?? {},
    message
  });
}
class AddToContextRequest {
  constructor(hub, address) {
    this.hub = hub;
    this.address = address;
  }
}
class AddedToContext {
}
function addToContext(context, hub, address) {
  const subscription = context.pipe(filterByTarget(address)).subscribe(hub);
  subscription.add(hub.pipe(addSender(address)).subscribe(context));
  subscription.add(hub.pipe(ofType(AddToContextRequest)).subscribe(({ message: { hub: hub2, address: address2 } }) => addToContext(context, hub2, address2)));
  sendMessage(hub, new AddedToContext());
  return subscription;
}
function contractMessage(type) {
  return function(constructor) {
    var _a;
    return _a = class extends constructor {
      constructor() {
        super(...arguments);
        this.$type = type;
      }
    }, _a.$type = type, _a;
  };
}
var getRandomValues;
var rnds8 = new Uint8Array(16);
function rng() {
  if (!getRandomValues) {
    getRandomValues = typeof crypto !== "undefined" && crypto.getRandomValues && crypto.getRandomValues.bind(crypto) || typeof msCrypto !== "undefined" && typeof msCrypto.getRandomValues === "function" && msCrypto.getRandomValues.bind(msCrypto);
    if (!getRandomValues) {
      throw new Error("crypto.getRandomValues() not supported. See https://github.com/uuidjs/uuid#getrandomvalues-not-supported");
    }
  }
  return getRandomValues(rnds8);
}
const REGEX = /^(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|00000000-0000-0000-0000-000000000000)$/i;
function validate(uuid) {
  return typeof uuid === "string" && REGEX.test(uuid);
}
var byteToHex = [];
for (var i = 0; i < 256; ++i) {
  byteToHex.push((i + 256).toString(16).substr(1));
}
function stringify(arr) {
  var offset = arguments.length > 1 && arguments[1] !== void 0 ? arguments[1] : 0;
  var uuid = (byteToHex[arr[offset + 0]] + byteToHex[arr[offset + 1]] + byteToHex[arr[offset + 2]] + byteToHex[arr[offset + 3]] + "-" + byteToHex[arr[offset + 4]] + byteToHex[arr[offset + 5]] + "-" + byteToHex[arr[offset + 6]] + byteToHex[arr[offset + 7]] + "-" + byteToHex[arr[offset + 8]] + byteToHex[arr[offset + 9]] + "-" + byteToHex[arr[offset + 10]] + byteToHex[arr[offset + 11]] + byteToHex[arr[offset + 12]] + byteToHex[arr[offset + 13]] + byteToHex[arr[offset + 14]] + byteToHex[arr[offset + 15]]).toLowerCase();
  if (!validate(uuid)) {
    throw TypeError("Stringified UUID is invalid");
  }
  return uuid;
}
function v4(options, buf, offset) {
  options = options || {};
  var rnds = options.random || (options.rng || rng)();
  rnds[6] = rnds[6] & 15 | 64;
  rnds[8] = rnds[8] & 63 | 128;
  if (buf) {
    offset = offset || 0;
    for (var i = 0; i < 16; ++i) {
      buf[offset + i] = rnds[i];
    }
    return buf;
  }
  return stringify(rnds);
}
var __defProp2 = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __decorateClass = (decorators, target, key, kind) => {
  var result = kind > 1 ? void 0 : kind ? __getOwnPropDesc(target, key) : target;
  for (var i = decorators.length - 1, decorator; i >= 0; i--)
    if (decorator = decorators[i])
      result = (kind ? decorator(target, key, result) : decorator(result)) || result;
  if (kind && result)
    __defProp2(target, key, result);
  return result;
};
let LayoutAddress = class {
  constructor(id) {
    this.id = id;
  }
};
LayoutAddress = __decorateClass([
  contractMessage("OpenSmc.Application.Layout.LayoutAddress")
], LayoutAddress);
let ConnectToHubRequest = class {
  constructor(from, to) {
    this.from = from;
    this.to = to;
  }
};
ConnectToHubRequest = __decorateClass([
  contractMessage("OpenSmc.Messaging.ConnectToHubRequest")
], ConnectToHubRequest);
let UiAddress = class {
  constructor(id) {
    this.id = id;
  }
};
UiAddress = __decorateClass([
  contractMessage("OpenSmc.Application.UiAddress")
], UiAddress);
let RefreshRequest = class {
  constructor(area) {
    this.area = area;
  }
};
RefreshRequest = __decorateClass([
  contractMessage("OpenSmc.Application.RefreshRequest")
], RefreshRequest);
let AreaChangedEvent = class {
  constructor(area, view, options, style) {
    this.area = area;
    this.view = view;
    this.options = options;
    this.style = style;
  }
};
AreaChangedEvent = __decorateClass([
  contractMessage("OpenSmc.Application.AreaChangedEvent")
], AreaChangedEvent);
let ClickedEvent = class {
  constructor(id, payload) {
    this.id = id;
    this.payload = payload;
  }
};
ClickedEvent = __decorateClass([
  contractMessage("OpenSmc.Application.Layout.Views.ClickedEvent")
], ClickedEvent);
let ExpandRequest = class {
  constructor(id, area, payload) {
    this.id = id;
    this.area = area;
    this.payload = payload;
  }
};
ExpandRequest = __decorateClass([
  contractMessage("OpenSmc.Application.Layout.Views.ExpandRequest")
], ExpandRequest);
let SetAreaRequest = class {
  constructor(area, path, options) {
    this.area = area;
    this.path = path;
    this.options = options;
  }
};
SetAreaRequest = __decorateClass([
  contractMessage("OpenSmc.Application.SetAreaRequest")
], SetAreaRequest);
let CategoryItemsRequest = class {
  constructor(categoryName, search, page, pageSize) {
    this.categoryName = categoryName;
    this.search = search;
    this.page = page;
    this.pageSize = pageSize;
  }
};
CategoryItemsRequest = __decorateClass([
  contractMessage("OpenSmc.Categories.CategoryItemsRequest")
], CategoryItemsRequest);
let CategoryItemsResponse = class {
  constructor(result, errorMessage) {
    this.result = result;
    this.errorMessage = errorMessage;
  }
};
CategoryItemsResponse = __decorateClass([
  contractMessage("OpenSmc.Categories.CategoryItemsResponse")
], CategoryItemsResponse);
let SetSelectionRequest = class {
  constructor(selection) {
    this.selection = selection;
  }
};
SetSelectionRequest = __decorateClass([
  contractMessage("OpenSmc.Categories.SetSelectionRequest")
], SetSelectionRequest);
let ErrorEvent = class {
  constructor(sourceEvent, message) {
    this.sourceEvent = sourceEvent;
    this.message = message;
  }
};
ErrorEvent = __decorateClass([
  contractMessage("OpenSmc.ErrorEvent")
], ErrorEvent);
let ModuleErrorEvent = class {
};
ModuleErrorEvent = __decorateClass([
  contractMessage("OpenSmc.ModuleErrorEvent")
], ModuleErrorEvent);
let CloseModalDialogEvent = class {
};
CloseModalDialogEvent = __decorateClass([
  contractMessage("OpenSmc.Application.CloseModalDialogEvent")
], CloseModalDialogEvent);
const ofContractType = (ctor) => filter((envelope) => envelope.message.$type === ctor.$type);
class ControlBase extends SubjectHub {
  constructor($type, id = v4()) {
    super();
    this.$type = $type;
    this.id = id;
    this.subscription = new Subscription();
    this.address = `${$type}-${id}`;
  }
  toJSON() {
    const {
      subscription,
      input,
      output,
      ...result
    } = this;
    return result;
  }
  withMessageHandler(type, handler) {
    this.subscription.add(
      this.handleMessage(type, handler)
    );
    return this;
  }
  sendMessage(message, target) {
    this.output.next({ message, target });
  }
  handleMessage(type, handler) {
    return this.input.pipe(ofContractType(type)).subscribe(handler.bind(this));
  }
  withId(id) {
    this.id = id;
    return this;
  }
  withAddress(address) {
    this.address = address;
    return this;
  }
  withData(data) {
    this.data = data;
    return this;
  }
  withDataContext(dataContext) {
    this.dataContext = dataContext;
    return this;
  }
  // withStyle(buildFunc: (builder: Style) => void) {
  //     this.style = buildFunc(makeStyle());
  //     return this;
  // }
  withClassName(value) {
    this.className = value;
    return this;
  }
  // withFlex(buildFunc?: (builder: StyleBuilder) => void) {
  //     this.data.styleBuilder = buildFunc?.(makeStyle().withDisplay("flex"))
  //     return this;
  // }
  withSkin(value) {
    this.skin = value;
    return this;
  }
  withClickMessage(message = { address: this.address, message }) {
    this.clickMessage = message;
    return this;
  }
  withTooltip(tooltip) {
    this.tooltip = tooltip;
    return this;
  }
  withLabel(label) {
    this.label = label;
    return this;
  }
  isReadOnly(isReadOnly) {
    this.isReadonly = isReadOnly;
    return this;
  }
}
const mainWindowAreas = {
  main: "Main",
  toolbar: "Toolbar",
  sideMenu: "SideMenu",
  contextMenu: "ContextMenu",
  modal: "Modal",
  statusBar: "StatusBar"
};
class LayoutStack extends ControlBase {
  constructor(id) {
    super("LayoutStackControl", id);
    this.areas = [];
  }
  withView(view, area = v4(), options, style) {
    this.areas.push(new AreaChangedEvent(area, view, options, style));
    return this;
  }
  withSkin(value) {
    return super.withSkin(value);
  }
  withHighlightNewAreas(value) {
    this.highlightNewAreas = value;
    return this;
  }
  withColumnCount(value) {
    this.columnCount = value;
    return this;
  }
  // addView(view: ControlBase, buildFunc?: (builder: AreaChangedEventBuilder) => void) {
  //     const are
  //     const builder = new AreaChangedEventBuilder<StackOptions>().withView(viewBuilder);
  //     buildFunc?.(builder);
  //
  //     if (!this.areas) {
  //         this.areas = [];
  //     }
  //
  //     const event = builder.build()
  //     const {view, area, options} = event;
  //
  //     const insertAfterArea = options?.insertAfter;
  //
  //     const insertAfterEvent =
  //         insertAfterArea ? this.areas.find(a => a.area === insertAfterArea) : null;
  //
  //     this.areas = insertAfter(this.areas, event, insertAfterEvent);
  //
  //     this.sendMessage(new AreaChangedEvent(area, view, options));
  // }
  //
  // removeView(area: string) {
  //     const index = this.areas?.findIndex(a => a.area === area);
  //
  //     if (index !== -1) {
  //         this.areas.splice(index, 1);
  //         this.setArea(area, null);
  //     }
  // }
}
class MainWindowStack extends LayoutStack {
  constructor(id) {
    super(id);
    this.withSkin("MainWindow");
  }
  withSideMenu(view) {
    return this.withView(view, mainWindowAreas.sideMenu);
  }
  withToolbar(view) {
    return this.withView(view, mainWindowAreas.toolbar);
  }
  withContextMenu(view) {
    return this.withView(view, mainWindowAreas.contextMenu);
  }
  withMain(view) {
    return this.withView(view, mainWindowAreas.main);
  }
  withStatusBar(view) {
    return this.withView(view, mainWindowAreas.statusBar);
  }
  withModal(view) {
    return this.withView(view, mainWindowAreas.modal);
  }
}
const makeStack = (id) => new LayoutStack(id);
class ExpandableControl extends ControlBase {
  constructor($type) {
    super($type);
  }
  withExpandMessage(expandMessage) {
    this.expandMessage = expandMessage;
    return this;
  }
}
class MenuItem extends ExpandableControl {
  constructor() {
    super("MenuItemControl");
  }
  withTitle(title) {
    this.title = title;
    return this;
  }
  withIcon(icon) {
    this.icon = icon;
    return this;
  }
  withColor(color) {
    this.color = color;
    return this;
  }
  withSkin(value) {
    return super.withSkin(value);
  }
}
const makeMenuItem = () => new MenuItem();
const rootDir = "./notebooks";
class PlaygroundWindow extends MainWindowStack {
  constructor() {
    super();
    this.withAddress("PlaygroundWindow");
    this.withSideMenu(this.makeSideMenu());
    this.withMessageHandler(ClickedEvent, ({ message }) => {
      this.sendMessage(message.payload, uiAddress);
    });
  }
  makeSideMenu() {
    const sideMenu = makeStack().withAddress("SideMenu");
    console.log(fs);
    const files = fs.readdirSync(rootDir, { withFileTypes: true });
    for (const file of files) {
      sideMenu.withView(
        makeMenuItem().withTitle(file.name).withClickMessage({
          address: this.address,
          message: new ClickedEvent(null, file.name)
        })
      );
    }
    return sideMenu;
  }
}
class LayoutHub extends SubjectHub {
  constructor() {
    super();
    __publicField(this, "controlsByArea", /* @__PURE__ */ new Map());
    __publicField(this, "subscription", new Subscription());
    this.subscription.add(
      this.handleMessage(
        SetAreaRequest,
        (envelope) => this.handleSetAreaRequest(envelope)
      )
    );
  }
  handleMessage(type, handler) {
    return this.input.pipe(ofContractType(type)).subscribe(handler.bind(this));
  }
  sendMessage(message, target) {
    this.output.next({ message, target });
  }
  handleSetAreaRequest({ message }) {
    const { area, path, options } = message;
    const address = this.controlsByArea.get(area);
    if (path) {
      if (!address) {
        const control = this.createControlByPath(path, options);
        this.controlsByArea.set(area, control);
        this.sendMessage(new AddToContextRequest(control, control.address));
        this.sendMessage(new AreaChangedEvent(area, control), uiAddress);
      }
    } else {
      this.controlsByArea.delete(area);
    }
  }
  createControlByPath(path, options) {
    switch (path) {
      case "/":
        return new PlaygroundWindow();
      default:
        throw `Unknown path "${path}"`;
    }
  }
}
function playgroundServer() {
  return {
    name: "playgroundServer",
    configureServer(server) {
      const { ws } = server;
      ws.on("connection", function(socket, request) {
        ws.clients.forEach((client) => {
          if (client.socket === socket) {
            const clientHub = new WebSocketClientHub(client, ws);
            const [uiHub, uiHubProxy] = makeProxy();
            connect(clientHub, uiHubProxy);
            const context = new Subject();
            addToContext(context, uiHub, uiAddress);
            addToContext(context, new LayoutHub(), layoutAddress);
          }
        });
      });
    }
  };
}
export {
  playgroundServer
};
