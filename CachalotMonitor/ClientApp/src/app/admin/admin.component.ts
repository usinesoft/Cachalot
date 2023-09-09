import { Component, OnDestroy, OnInit } from "@angular/core";
import { interval, Subscription } from "rxjs";
import { AdminService } from "../admin.service";
import { Process } from "../model/backup";
import { MonitoringService } from "../monitoring.service";
import { QueryService } from "../query.service";

@Component({
  selector: "app-admin",
  templateUrl: "./admin.component.html",
  styleUrls: ["./admin.component.scss"]
})
export class AdminComponent implements OnInit, OnDestroy {


  private timerSubscription: Subscription | undefined;

  constructor(private service: AdminService,
    private monitoringService: MonitoringService,
    private queryService: QueryService) {
  }

  ngOnInit(): void {

    // filter out system tables (starting with @)
    this.collections = this.monitoringService.clusterInformation.getValue()?.schema.map(s => s.collectionName)
      .filter(c => c[0] != "@") ??
      [];

    this.service.getBackupDirectory().subscribe(data => {
      this._backupPath = data.backupDirectory;
      if (this._backupPath) {
        this.service.getBackupList().subscribe(data => {
            this.backupList = data.slice(0, 10);
            this.working = false;
          },
          err => this.working = false);
      }
    });

    // init the timer
    let tick = 0;
    this.timerSubscription = interval(400)
      .subscribe(x => {
        this.onFastTimer();

        if (tick % 5 == 0) {
          this.onSlowTimer();
        }

        tick++;
      });
  }

  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
  }


  working = false;

  saveBackupPath() {
    if (this._backupPath) {
      this.working = true;
      this.service.saveBackupDirectory(this._backupPath).subscribe(res => {
          this.service.getBackupDirectory().subscribe(data => {
              this._backupPath = data.backupDirectory;
              this.service.getBackupList().subscribe(data => {
                  this.backupList = data;
                  this.working = false;
                },
                err => this.working = false);
            },
            err => this.working = false);
        },
        err => this.working = false);
    }

  }


  private _backupPath: string | undefined;

  get backupPath(): string | undefined {
    return this._backupPath;
  }

  set backupPath(v: string | undefined) {
    this._backupPath = v;
  }


  backupList: string[] = [];

  selectedBackup: string | undefined;

  backup() {
    console.log("backup called");
    this.working = true;
    this.service.backup();
  }

  restore() {
    this.working = true;
    this.service.restore(this.selectedBackup!);
  }

  recreate() {
    this.working = true;
    this.service.recreate(this.selectedBackup!);
  }

  removeFromHistory(processId: string | undefined) {
    if (processId) {
      this.service.deleteProcess(processId);
    }

  }

  processHistory: Process[] = [];

  //every 400 ms
  private onFastTimer(): void {

  }

  // every 2 seconds
  private onSlowTimer(): void {
    // retrieve process history 
    this.service.getProcessHistory().subscribe(data => {
      var running = data.filter(x => x.status == "Running").length;

      this.working = running > 0;

      this.processHistory = data.slice(0, 12);
    });

    // retrieve backup list
    this.service.getBackupList().subscribe(data => {
      this.backupList = data;

    });
  }


  identifyProcess(index: Number, process: Process) {
    return process.processId;
  }

  selectedCollection: string | undefined;

  collections: string[] = [];


  actionToConfirm: string | undefined;

  confirm() {
    if (this.actionToConfirm == "drop") {
      this.drop();
      this.actionToConfirm = undefined;
      return;
    }
    if (this.actionToConfirm == "truncate") {
      this.truncate();
      this.actionToConfirm = undefined;
      return;
    }
    if (this.actionToConfirm == "restore") {
      this.restore();
      this.actionToConfirm = undefined;
      return;
    }
    if (this.actionToConfirm == "recreate") {
      this.recreate();
      this.actionToConfirm = undefined;
      return;
    }
  }

  drop() {
    this.working = true;
    this.service.dropDatabase().subscribe(x => {
      this.working = false;
    });
  }

  truncate() {
    this.working = true;
    this.service.truncate(this.selectedCollection!).subscribe(x => {
      this.working = false;
    });


  }


}
