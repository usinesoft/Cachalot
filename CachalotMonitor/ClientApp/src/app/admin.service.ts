import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BackupConfig, Process } from './model/backup';

@Injectable({
  providedIn: 'root'
})
export class AdminService {

  // initialization
  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }



  public working: boolean = false;

  public getBackupDirectory(): Observable<BackupConfig> {
    return this.http.get<BackupConfig>(this.baseUrl + 'Admin/backup/path');
  }

  public getBackupList(): Observable<string[]> {
    return this.http.get<string[]>(this.baseUrl + 'Admin/backup/list');
  }

  public saveBackupDirectory(directory: string): Observable<any> {

    var cfg = new BackupConfig;
    cfg.backupDirectory = directory;

    return this.http.post<any>(this.baseUrl + 'Admin/backup/path', cfg);

  }

  public getProcessHistory() {
    return this.http.get<Process[]>(this.baseUrl + 'Admin/process/list');
  }

  public backup() {
    this.http.post<any>(this.baseUrl + 'Admin/backup/save', null).subscribe(_data => console.log('backup started'));
  }

  public restore(backup: string) {
    this.http.post<any>(this.baseUrl + `Admin/backup/restore/${backup}`, null).subscribe(_data => console.log('restore started'));
  }

  public recreate(backup: string) {
    this.http.post<any>(this.baseUrl + `Admin/backup/recreate/${backup}`, null).subscribe(_data => console.log('recreate started'));
  }

  public deleteProcess(id: string) {
    this.http.delete<any>(this.baseUrl + `Admin/process/delete/${id}`).subscribe(_data => console.log('delete started'));
  }

  public dropDatabase():Observable<any>{
    return this.http.delete<any>(this.baseUrl + `Admin/drop`);
  }

  public truncate(collection:string):Observable<any>{
    return this.http.delete<any>(this.baseUrl + `Admin/truncate/${collection}`);
  }
}
