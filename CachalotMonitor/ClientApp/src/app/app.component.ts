import { Component } from '@angular/core';
import { MonitoringService } from './monitoring.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent {
  title = 'app';
  isExpanded = true;
  showSubmenu: boolean = false;
  isShowing = false;
  showSubSubMenu: boolean = false;

  mouseenter() {
    if (!this.isExpanded) {
      this.isShowing = true;
    }
  }

  mouseleave() {
    if (!this.isExpanded) {
      this.isShowing = false;
    }
  }

  public cluster:string|null = null;

  constructor(public service: MonitoringService) {
    service.currentCluster.subscribe(data=>{
      this.cluster = data;
    });
  }
}
