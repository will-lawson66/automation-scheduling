# Scheduler Application 

## Software assemblies/components for scheduler application
- CMR
    
- Data persistence

- Comms
    - gRPC Gateway 
        
- Workflow management
    - Process manager 
    - Stateless state

- Application Services
    - Assay manager service        
    - Assay execution planner service        
    - SequenceGroup manager service        
    - Hardware execution planner service        
    - Inventory service
- Models
    - CMR model
    - AssaySample models
        - see design doc
    - 
- Other features
    - Event management
        - rich event-driven-architectural components to consume hardware events and issue scheduling events
    - FLR context
        - the FLR is the aggregate result of an assay run/CMR
        - there will be a stateful component to manage the FLR context for all assays

