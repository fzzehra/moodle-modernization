@mod @mod_assign
Feature: Capstone assignment and grade characterization
  Background:
    Given the following "courses" exist:
      | fullname | shortname | format | numsections |
      | WBS Course | WBS | topics | 3 |
    And the following "users" exist:
      | username | firstname | lastname | email |
      | teacher1 | Teacher | One | teacher1@example.com |
      | student1 | Student | One | student1@example.com |
    And the following "course enrolments" exist:
      | user | course | role |
      | teacher1 | WBS | editingteacher |
      | student1 | WBS | student |

  Scenario: T-ASG-02 teacher creates an assignment visible to the student
    Given I log in as "teacher1"
    When I add an assign activity to course "WBS Course" section "1" and I fill the form with:
      | Assignment name | Created assignment |
    Then I should see "Created assignment"
    And I log out
    And I log in as "student1"
    And I am on "WBS Course" course homepage
    And I should see "Created assignment"

  @javascript
  Scenario: T-ASG-01 text persists and confirmation controls the submission state
    Given the following "activities" exist:
      | activity | course | name | assignsubmission_onlinetext_enabled | submissiondrafts |
      | assign | WBS | Confirmation off | 1 | 0 |
      | assign | WBS | Confirmation on | 1 | 1 |
    And I am on the "Confirmation off" Activity page logged in as student1
    When I press "Add submission"
    And I set the field "Online text" to "Stored capstone text"
    And I press "Save changes"
    And I am on the "Confirmation off" Activity page
    Then I should see "Submitted for grading"
    And I should see "Stored capstone text"
    And I am on the "Confirmation on" Activity page
    And I press "Add submission"
    And I set the field "Online text" to "Stored capstone text"
    And I press "Save changes"
    And I am on the "Confirmation on" Activity page
    And I should see "Draft (not submitted)"
    And I should see "Stored capstone text"
    And I press "Submit assignment"
    And I press "Continue"
    And I should see "Submitted for grading"

  @javascript
  Scenario: T-ASG-04 cutoff closes the submission without an extension
    Given the following "activity" exists:
      | activity | assign |
      | course | WBS |
      | name | Closed assignment |
      | assignsubmission_onlinetext_enabled | 1 |
    And I am on the "Closed assignment" Activity page logged in as teacher1
    And I navigate to "Settings" in current page administration
    And I click on "Expand all" "link" in the "region-main" "region"
    And I set the field "Allow submissions from" to "##3 days ago##"
    And I set the field "Due date" to "##2 days ago##"
    And I set the field "Cut-off date" to "##yesterday noon##"
    And I press "Save and return to course"
    And I log out
    When I am on the "Closed assignment" Activity page logged in as student1
    Then "Add submission" "button" should not exist

  @javascript
  Scenario: T-GRD-01 and T-GRD-04 save a valid grade and reject a value above the maximum
    Given the following "activities" exist:
      | activity | course | name | grade | assignsubmission_onlinetext_enabled |
      | assign | WBS | Numeric assignment | 100 | 1 |
    And the following "mod_assign > submissions" exist:
      | assign | user | onlinetext |
      | Numeric assignment | student1 | Ready to grade |
    And I am on the "Numeric assignment" Activity page logged in as teacher1
    And I change window size to "large"
    And I go to "Student One" "Numeric assignment" activity advanced grading page
    When I set the field "Grade out of 100" to "0"
    And I press "Save changes"
    And I am on the "Numeric assignment" Activity page
    And I go to "Student One" "Numeric assignment" activity advanced grading page
    Then the field "Grade out of 100" matches value "0.00"
    And I set the field "Grade out of 100" to "-1"
    And I press "Save changes"
    And I should see "Grade must be greater than or equal to zero."
    And I am on the "Numeric assignment" Activity page
    And I go to "Student One" "Numeric assignment" activity advanced grading page
    And the field "Grade out of 100" matches value "0.00"
    And I set the field "Grade out of 100" to "100"
    And I press "Save changes"
    And I am on the "Numeric assignment" Activity page
    And I go to "Student One" "Numeric assignment" activity advanced grading page
    Then the field "Grade out of 100" matches value "100.00"
    And I set the field "Grade out of 100" to "101"
    And I press "Save changes"
    And I should see "Grade must be less than or equal to 100."
    And I am on the "Numeric assignment" Activity page
    And I go to "Student One" "Numeric assignment" activity advanced grading page
    And the field "Grade out of 100" matches value "100.00"
    And I log out
    And I am on the "WBS Course" "grades > User report > View" page logged in as "student1"
    And I should see "100.00" in the "Numeric assignment" "table_row"
