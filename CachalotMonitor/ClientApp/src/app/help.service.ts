import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class HelpService {

  constructor() { }

  
  public detailMode:boolean = true;


  public tooltipsSmall:any = {
    'admin.backup': `
      Backup the database.            
    `,
    'admin.drop.database':`    
      Drop the database
    `,
    'admin.drop.collection':`    
      Remove a collection
    `,
    'admin.truncate.collection':`    
      Delete all data
    `,
    'admin.readonly':`    
      Switch to read-only mode
    `
    ,
    'admin.backup.dir':`    
      Set as backup path      
    `
    ,
    'admin.backup.restore':`    
      Restore from backup      
    `
    ,
    'admin.backup.feed':`    
      Feed from backup      
    `
    ,
    'data.refresh':`    
      Refresh the query result
    `
    ,
    'data.import':`    
      Import data as JSON
    `
    ,
    'data.export':`    
      Export data as JSON
    `
    ,
    'data.ignore.take':`    
      Ignore TAKE clause for export
    `
    ,
    'data.delete':`    
      Delete the query result !!!
    `
    ,
    'schema.upgrade':`    
      Upgrade the index
    `
    ,
    'layout.default':`    
      Default layout
    `
    ,
    'layout.compressed':`    
      Compressed layout
    `
    ,
    'layout.flat':`    
      Flat layout 
    `

  }

  public tooltipsDetail:any = {
    'admin.backup': `
      <h3>Backup the database</h3>    
      <div class="tooltip-left">
        A compressed backup will be created in a folder under the <b>Backup directory</b>
      </div>
      <br>  
      (admin only)
    `,
    'admin.drop.database':`    
      <h3>Drop the database</h3>
      <div class="tooltip-left">
        All data and schema information will be deleted      
      </div>
      <br>   
      (admin only)
    `,
    'admin.readonly':`    
      <h3>Switch to read-only mode</h3>
      <div class="tooltip-left">
        The operations that modify data or schema will fail  
      </div>
      <br>   
      (admin only)
    `,
    'admin.backup.dir':`    
      <h3>Set as backup path</h3>
      <div class="tooltip-left">         
        The backups will be created and looked-up here
      </div>
      <br>   
      (admin only)
    `,
    'admin.backup.explain':`    
      The backup directory must be a network folder mounted under the same path on all the servers in the cluster.      
      <br>   
      All the cache nodes backup (and restore) their data in parallel.
    `,
    'admin.backup.restore':`    
      <h3>Restore from backup.</h3>
      <div class="tooltip-left">         
      Restore the same cluster to a previous state or initialize another identical cluster (<b>same</b> number of nodes).
      </div>
      <br>   
      (admin only)
    `,
    'admin.truncate.collection':`    
      <h3>Truncate the collection.</h3>
      <div class="tooltip-left">         
      All data is deleted. The collection will be empty but schema information is preserved.
      </div>
      <br>   
      (admin only)
    `,
    'admin.drop.collection':`    
      <h3>Drop the collection.</h3>
      <div class="tooltip-left">         
      All data and schema information is deleted.
      </div>
      <br>   
      (admin only)
    `,
    'admin.backup.feed':` 
    <h3>Feed from backup.</h3>
    <div class="tooltip-left">         
      <p>Initialize a cluster with data from a backup. The target can have a <b>different</b> number of nodes. </p>
      <br>
      Use it to copy a cluster to a different one or to add nodes to an existing cluster. In this case
      <ul>
        <li>Backup the cluster</li>
        <li>Drop the database</li>
        <li>Add nodes</li>
        <li>Feed from backup</li>
      </ul>
      <br>   
    </div>
      (admin only)
    `
    
    ,
    'data.import':`    
    <h3>Import data from a JSON file.</h3>
    <div class="tooltip-left">         
      <p>The file may contain a single object or a JSON array</p>
    </div>
    (admin only)
    `
    ,
    'data.export':`    
    <h3>Export as JSON</h3>
    <div class="tooltip-left">         
      <p>Export the result of the current query to a JSON file.</p>
      <p>If the query is empty and <b>ignore limit</b> is checked, the whole collection will be exported.</p>
    </div>
    `
    ,
    'data.ignore.take':`    
    <h3>Ignore the TAKE clause</h3>
    <div class="tooltip-left">         
      <p>Applies to data export.</p>
      <p>With an empty query all the collection will be exported.</p>
    </div>
    `
    ,
    'data.delete':`    
    <h3>Delete the query result</h3>
    <div class="tooltip-left">         
      <p>All the items matching the current query will be deleted !!!</p>
      <p>The TAKE clause is <b>ignored</b>.</p>
      <p>The WHERE clause can not be empty. To delete the whole content of the collection use <b>Truncate</b> in the Admin page</p>
    </div>
    (admin only)
    `
    ,
    'schema.upgrade':`    
    <h3>Upgrade the index</h3>
    <div class="tooltip-left">         
      <p>Any queryable property can be indexed</p>
      <p>The primary key is always indexed. No upgrade is possible</p>
      <p>A non-indexed property can be upgraded to <b>dictionary index</b></p>
      <p>A dictionary indexed can be upgraded to <b>ordered index</b></p>
      <br>
      Ordered indexes are required to enable server-side ORDER BY clause.  
      They can also be used by queries with comparison operators.
      Insertions are more expensive.  
    </div>
    `
    ,
    'layout.default':`    
    <h3>Default layout</h3>
    <div class="tooltip-left">         
    <p>
    Some properties at the root level can be used for queries.
    They must be scalar properties or collections of scalar properties.
    </p>
      <p>The whole object is stored as JSON.</p>
      <p>Ideal for small/medium objects.</p>      
    </div>
    `
    ,
    'layout.compressed':`    
    <h3>Compressed layout</h3>
    <div class="tooltip-left">         
      <p>
      Some properties at the root level can be used for queries.
      They must be scalar properties or collections of scalar properties.
      </p>
      <p>The whole object is stored as <b>compressed</b> JSON.</p>
      <p>Ideal for medium/big objects.</p>      
    </div>
    `
    ,
    'layout.flat':`    
    <h3>Flat layout</h3>
    <div class="tooltip-left">         
      <p>Can be used only for flat object (having only scalar properties at the root level, no tree structure). Like lines of a CSV file or lines in an SQL database table</p>
      <p>It offers the smallest memory footprint for this case.</p>
      <p>All properties are queryable.</p>      
    </div>
    `
  }

  public tooltip(code:string):string|undefined{
    if(this.detailMode)
      return this.tooltipsDetail[code] ?? this.tooltipsSmall[code];

      return this.tooltipsSmall[code];
  }
  

}
