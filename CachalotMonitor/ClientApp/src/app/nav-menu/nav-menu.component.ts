import { Component } from '@angular/core';
import { MonitoringService } from '../monitoring.service';

@Component({
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.css']
})
export class NavMenuComponent {
  
  isExpanded = false;



  // public get connected(): boolean {

  //   if (!this.service.clusterInfo) {
  //     return false;
  //   }

  //   return this.service.clusterInfo?.status != 'NotConnected';
  // }


  constructor(public service: MonitoringService) {

  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
