import { Injectable } from '@angular/core';
import {
    HttpInterceptor,
    HttpRequest,
    HttpHandler,
    HttpEvent,
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConnectionService } from './connection.service';

@Injectable()
export class MyInterceptor implements HttpInterceptor {

    constructor(private connectionService:ConnectionService) { }
    intercept(
        request: HttpRequest<any>,
        next: HttpHandler
    ): Observable<HttpEvent<any>> {

        var code = this.connectionService.adminCode;

        if(code){
            const modifiedRequest = request.clone({
                headers: request.headers.set('x-token', code),
            });
            return next.handle(modifiedRequest);
        }
        
        return next.handle(request);
    }
}