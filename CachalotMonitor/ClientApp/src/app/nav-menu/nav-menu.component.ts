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


  public cluster:string|null = null;

  constructor(public service: MonitoringService) {
    service.currentCluster.subscribe(data=>{
      this.cluster = data;
    });
  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
