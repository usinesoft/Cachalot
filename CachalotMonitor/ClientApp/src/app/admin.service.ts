import { HttpClient } from "@angular/common/http";
import { Inject, Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { BackupConfig, Process } from "./model/backup";

@Injectable({
  providedIn: "root"
})
export class AdminService {

  // initialization
  constructor(private http: HttpClient, @Inject("BASE_URL") private baseUrl: string) {}


  working = false;

  getBackupDirectory(): Observable<BackupConfig> {
    return this.http.get<BackupConfig>(this.baseUrl + "Admin/backup/path");
  }

  getBackupList(): Observable<string[]> {
    return this.http.get<string[]>(this.baseUrl + "Admin/backup/list");
  }

  saveBackupDirectory(directory: string): Observable<any> {

    const cfg = new BackupConfig;
    cfg.backupDirectory = directory;

    return this.http.post<any>(this.baseUrl + "Admin/backup/path", cfg);

  }

  switchToReadOnly(){
    return this.http.post<any>(this.baseUrl + "Admin/read-only", null);
  }

  switchToReadWrite(){
    return this.http.post<any>(this.baseUrl + "Admin/read-write", null);
  }


  getProcessHistory() {
    return this.http.get<Process[]>(this.baseUrl + "Admin/process/list");
  }

  backup() {
    this.http.post<any>(this.baseUrl + "Admin/backup/save", null).subscribe(_data => console.log("backup started"));
  }

  restore(backup: string) {
    this.http.post<any>(this.baseUrl + `Admin/backup/restore/${backup}`, null)
      .subscribe(_data => console.log("restore started"));
  }

  recreate(backup: string) {
    this.http.post<any>(this.baseUrl + `Admin/backup/recreate/${backup}`, null)
      .subscribe(_data => console.log("recreate started"));
  }

  deleteProcess(id: string) {
    this.http.delete<any>(this.baseUrl + `Admin/process/delete/${id}`)
      .subscribe(_data => console.log("delete started"));
  }

  dropDatabase(): Observable<any> {
    return this.http.delete<any>(this.baseUrl + `Admin/drop`);
  }

  truncate(collection: string): Observable<any> {
    return this.http.delete<any>(this.baseUrl + `Admin/truncate/${collection}`);
  }

  dropCollection(collection: string): Observable<any> {
    return this.http.delete<any>(this.baseUrl + `Admin/drop/${collection}`);
  }
}
