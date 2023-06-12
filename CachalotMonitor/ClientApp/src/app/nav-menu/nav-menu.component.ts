import { Component } from '@angular/core';
import { MonitoringService } from '../monitoring.service';

@Component({
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.scss']
})
export class NavMenuComponent {
  
  isExpanded = false;


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
