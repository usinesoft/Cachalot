import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({
  providedIn: 'root'
})
export class ConnectionService {

  constructor(private http: HttpClient, private snackBar: MatSnackBar, @Inject("BASE_URL") private baseUrl: string){ }


  public isAdmin:boolean = false;

  public adminCode:string|undefined;

  displayError(message: string, detail?: string): void {
    let fullMessage = message;
    if (detail) {
      fullMessage += `:${detail}`;
    }
    this.snackBar.open(fullMessage, "", { duration: 3000, panelClass: "red-snackbar" });
  }

  public connectAsAdmin(adminCode:string){
    this.http.get<any>(this.baseUrl + "Admin/check-code/" + adminCode).subscribe(result => {

      if(result.isValid){
        this.isAdmin = true;
        this.adminCode = adminCode;
      }
      else{
        this.isAdmin = false;
        this.adminCode = undefined;
        this.displayError("Invalid admin code");
      }
      
  },
  err=>{
        this.isAdmin = false;
        this.displayError(err ?? "connection error");
  });
  
  }

  public disconnectAdmin(){
    this.adminCode = undefined;
    this.isAdmin = false;
  }

}
