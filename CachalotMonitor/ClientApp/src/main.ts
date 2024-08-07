import { enableProdMode } from "@angular/core";
import { platformBrowserDynamic } from "@angular/platform-browser-dynamic";

import { AppModule } from "./app/app.module";
import { environment } from "./environments/environment";

export function getBaseUrl() {

  // harcdcoded in development environment
  if (environment.rootUrl) {
    return environment.rootUrl;
  }


  const root = `${window.location.protocol}//${window.location.hostname}:${window.location.port}/`;

  console.log(`root=${root}`);


  return root;
  //return document.getElementsByTagName('base')[0].href;
}

const providers = [
  { provide: "BASE_URL", useFactory: getBaseUrl, deps: [] }
];

if (environment.production) {
  enableProdMode();
}

platformBrowserDynamic(providers).bootstrapModule(AppModule)
  .catch(err => console.log(err));
